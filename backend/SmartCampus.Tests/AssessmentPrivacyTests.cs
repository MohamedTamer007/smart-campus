using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class AssessmentPrivacyTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class Boundary(Func<Task>? duringRequest = null) : HttpMessageHandler, IHttpClientFactory
    {
        public int Requests { get; private set; }
        public string? Payload { get; private set; }
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            Payload = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (duringRequest is not null) await duringRequest();
            return new(HttpStatusCode.OK) { Content = new StringContent("""{"choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Synthetic suggestion\"}"}}]}""", Encoding.UTF8, "application/json") };
        }
    }
    private WebApplicationFactory<Program> Configured() => campus.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
        config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["AI:Provider"] = "openrouter", ["AI:AllowNonSensitiveInference"] = "true", ["AI:AllowNonSensitiveImages"] = "true",
            ["AI:OpenRouterKey"] = "synthetic-not-a-provider-key", ["AI:FaultTypes:0"] = "plumbing",
            ["AI:Priorities:0"] = "urgent", ["AI:Risks:0"] = "high"
        })));
    private async Task<HttpClient> Login(WebApplicationFactory<Program> host, Guid actor)
    {
        var client = host.CreateClient();
        await CampusFixture.Csrf(client);
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(actor), password = campus.Password }));
        await CampusFixture.Csrf(client);
        return client;
    }
    private static Task<HttpResponseMessage> Review(HttpClient client, Guid report, int version = 1, bool confirmed = true, bool image = false) =>
        client.PostAsJsonAsync($"/api/v1/maintenance/{report}/ai-review", new { version, confirmedNonSensitive = confirmed, includeImage = image });
    private static MaintenanceAssessmentProcessor Processor(IServiceProvider services, Boundary boundary) => new(
        services.GetRequiredService<CampusDb>(), services.GetRequiredService<PrivateMedia>(),
        new AiProviders(boundary, services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IInferenceBudget>()),
        services.GetRequiredService<IConfiguration>());

    [DatabaseFact]
    public async Task Unreviewed_report_never_reaches_external_provider()
    {
        using var host = Configured();
        using var owner = await Login(host, campus.Student);
        var id = await campus.Report(owner);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var job = await db.Jobs.SingleAsync(j => j.EntityId == id);
        using var boundary = new Boundary();
        var error = await Assert.ThrowsAsync<RuleException>(() => Processor(scope.ServiceProvider, boundary).Process(job, CancellationToken.None));
        Assert.Equal("ai_input_review_required", error.Code);
        Assert.Equal(0, boundary.Requests);
    }

    [DatabaseFact]
    public async Task Ai_review_requires_current_scoped_staff_explicit_confirmation_and_version()
    {
        using var host = Configured();
        using var owner = await Login(host, campus.Student);
        using var outsider = await Login(host, campus.Outsider);
        using var staff = await Login(host, campus.Staff);
        var id = await campus.Report(owner);
        Assert.Equal(HttpStatusCode.Forbidden, (await Review(owner, id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Review(outsider, id)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Review(staff, id, confirmed: false)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Review(staff, id, version: 0)).StatusCode);
        var reviewed = await CampusFixture.Response(await Review(staff, id));
        Assert.True(reviewed.GetProperty("assessment").GetProperty("inputReviewed").GetBoolean());
        Assert.False(reviewed.GetProperty("canReviewAiInput").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await Review(staff, id, version: 2)).StatusCode);
        using var scope = host.Services.CreateScope();
        var assessment = await scope.ServiceProvider.GetRequiredService<CampusDb>().Assessments.SingleAsync(a => a.ReportId == id);
        Assert.Equal(campus.Staff, assessment.ReviewedBy);
        Assert.Null(assessment.ReviewedImageId);
    }

    [DatabaseFact]
    public async Task Reviewed_analysis_stays_private_and_never_sets_staff_decision()
    {
        using var host = Configured();
        using var owner = await Login(host, campus.Student);
        using var staff = await Login(host, campus.Staff);
        var id = await campus.Report(owner);
        await CampusFixture.Response(await Review(staff, id));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        using var boundary = new Boundary();
        await Processor(scope.ServiceProvider, boundary).Process(await db.Jobs.SingleAsync(j => j.EntityId == id), CancellationToken.None);
        await db.SaveChangesAsync();
        Assert.Equal(1, boundary.Requests);
        Assert.DoesNotContain("image_url", boundary.Payload!);
        var ownerView = await CampusFixture.Response(await owner.GetAsync($"/api/v1/maintenance/{id}"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, ownerView.GetProperty("assessment").ValueKind);
        var staffView = await CampusFixture.Response(await staff.GetAsync($"/api/v1/maintenance/{id}"));
        Assert.Equal("completed", staffView.GetProperty("assessment").GetProperty("state").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, staffView.GetProperty("decision").GetProperty("type").ValueKind);
        Assert.Equal("submitted", staffView.GetProperty("status").GetString());
    }

    [DatabaseFact]
    public async Task Image_is_forwarded_only_after_explicit_approval_of_the_attached_image()
    {
        using var host = Configured();
        using var owner = await Login(host, campus.Student);
        using var staff = await Login(host, campus.Staff);
        var id = await campus.Report(owner);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        using var bitmap = new SkiaSharp.SKBitmap(4, 4);
        bitmap.Erase(SkiaSharp.SKColors.Blue);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var bytes = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(bytes.ToArray());
        var metadata = await scope.ServiceProvider.GetRequiredService<PrivateMedia>().Upload(
            new Microsoft.AspNetCore.Http.FormFile(stream, 0, stream.Length, "photo", "synthetic.png"), new() { ReportId = id, Bucket = "maintenance-images" });
        db.Media.Add(metadata);
        await db.SaveChangesAsync();
        await CampusFixture.Response(await Review(staff, id, image: true));
        db.ChangeTracker.Clear();
        var assessment = await db.Assessments.SingleAsync(a => a.ReportId == id);
        Assert.Equal(metadata.Id, assessment.ReviewedImageId);
        using var boundary = new Boundary();
        await Processor(scope.ServiceProvider, boundary).Process(await db.Jobs.SingleAsync(j => j.EntityId == id), CancellationToken.None);
        Assert.Contains("image_url", boundary.Payload!);
        Assert.Contains("data:image/jpeg;base64,", boundary.Payload!);
    }

    [DatabaseFact]
    public async Task Review_cannot_requeue_a_job_while_a_worker_holds_its_lease()
    {
        using var host = Configured();
        using var owner = await Login(host, campus.Student);
        using var staff = await Login(host, campus.Staff);
        var id = await campus.Report(owner);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var job = await db.Jobs.SingleAsync(j => j.EntityId == id);
        job.State = "running";
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await Review(staff, id)).StatusCode);
        db.ChangeTracker.Clear();
        Assert.Null((await db.Assessments.SingleAsync(a => a.ReportId == id)).ReviewedAt);
        Assert.Equal("running", (await db.Jobs.SingleAsync(j => j.EntityId == id)).State);
    }

    [DatabaseFact]
    public async Task Edited_report_invalidates_approval_and_late_analysis()
    {
        using var host = Configured();
        using var owner = await Login(host, campus.Student);
        using var staff = await Login(host, campus.Staff);
        var id = await campus.Report(owner);
        await CampusFixture.Response(await Review(staff, id));
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        using var boundary = new Boundary(async () => {
            using var form = new MultipartFormDataContent {
                { new StringContent("Changed synthetic fault"), "description" }, { new StringContent(campus.Faculty.ToString()), "faculty" },
                { new StringContent(campus.Building.ToString()), "building" }, { new StringContent(campus.Floor.ToString()), "floor" },
                { new StringContent("Changed synthetic location detail"), "details" }, { new StringContent("2"), "version" }
            };
            await CampusFixture.Response(await owner.PutAsync($"/api/v1/maintenance/{id}", form));
        });
        await Processor(scope.ServiceProvider, boundary).Process(await db.Jobs.SingleAsync(j => j.EntityId == id), CancellationToken.None);
        await db.SaveChangesAsync();
        var old = await db.Assessments.SingleAsync(a => a.ReportId == id && a.InputVersion == 1);
        Assert.Equal("input_changed", old.FailureCode);
        Assert.Null(old.FaultType);
        using var fresh = host.Services.CreateScope();
        var freshDb = fresh.ServiceProvider.GetRequiredService<CampusDb>();
        var current = await freshDb.Assessments.SingleAsync(a => a.ReportId == id && a.InputVersion == 2);
        Assert.Null(current.ReviewedAt);
        using var forbiddenBoundary = new Boundary();
        await Assert.ThrowsAsync<RuleException>(() => Processor(fresh.ServiceProvider, forbiddenBoundary).Process(
            freshDb.Jobs.Single(j => j.EntityId == id && j.InputVersion == 2), CancellationToken.None));
        Assert.Equal(0, forbiddenBoundary.Requests);
    }
}
