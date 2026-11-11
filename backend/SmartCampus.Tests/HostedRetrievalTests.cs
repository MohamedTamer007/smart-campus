using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

/// <summary>Never sends real people, campus photos or credentials to a hosted AI vendor.</summary>
public sealed class HostedRetrievalTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class SyntheticBoundary : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("retrieval", name);
            return new HttpClient(this, disposeHandler: false);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            Assert.Equal("https://openrouter.ai/api/v1/embeddings", request.RequestUri!.ToString());
            using var input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            var root = input.RootElement;
            Assert.Equal(RemoteEmbeddings.OpenRouterModel, root.GetProperty("model").GetString());
            var description = root.GetProperty("input").GetString();
            Assert.NotNull(description); // no image can leave this test process
            float[] vector = new float[RemoteEmbeddings.Dimension];
            vector[description.Contains("Blue", StringComparison.Ordinal) ? 0 :
                description.Contains("Red", StringComparison.Ordinal) ? 1 : 2] = 1;
            var response = new { model = RemoteEmbeddings.OpenRouterModel,
                data = new[] { new { index = 0, embedding = vector } } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }
    }

    private sealed class NoOpBudget : IInferenceBudget
    {
        public Task Reserve(string provider, CancellationToken cancellation)
        {
            Assert.Equal("openrouter", provider);
            return Task.CompletedTask;
        }
    }

    [DatabaseFact]
    public async Task Hosted_description_only_indexing_and_search_work_without_worker_or_photo_disclosure()
    {
        using var uploader = await campus.Login(campus.Student);
        var blue = await campus.Item(uploader);
        var red = await campus.Item(uploader);

        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        (await db.Items.FindAsync(blue))!.Description = "Blue distinctive bottle";
        (await db.Items.FindAsync(red))!.Description = "Red bag";
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, campus.Other.ToString())], "synthetic-test"));
        var access = new Access(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>(),
            scope.ServiceProvider.GetRequiredService<ReleasePolicy>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Search:Provider"] = "hosted",
            ["Search:ExternalProcessingApproved"] = "true", // synthetic test only
            ["Search:ExternalImagesApproved"] = "false",
            ["Search:MinimumScore"] = "0.9",
            ["Search:CalibrationApproval"] = "TEST ONLY — not a real score approval",
            ["Search:CalibrationRevision"] = "nemotron35-8b-openrouter-1024-v1",
            ["AI:OpenRouterKey"] = "synthetic-not-real-key",
            ["AI:OpenRouterDailyRequestLimit"] = "50",
            ["AI:OpenRouterRequestsPerMinute"] = "20"
        }).Build();

        using var boundary = new SyntheticBoundary();
        var remote = new RemoteEmbeddings(boundary, config, new NoOpBudget());
        var retrieval = new RetrievalService(db, access, boundary, config,
            scope.ServiceProvider.GetRequiredService<PrivateMedia>(), remote);
        await retrieval.Index(blue);
        await retrieval.Index(red);
        await db.Jobs.Where(j => j.Kind == "index" && (j.EntityId == blue || j.EntityId == red))
            .ExecuteUpdateAsync(update => update.SetProperty(j => j.State, "completed"));

        var response = JsonSerializer.SerializeToElement(await retrieval.Search("Blue distinctive bottle", null));
        Assert.Equal(blue, Assert.Single(response.GetProperty("itemIds").EnumerateArray()).GetGuid());
        Assert.Equal(RemoteEmbeddings.Revision(config), response.GetProperty("rankingVersion").GetString());
        Assert.Empty(JsonSerializer.SerializeToElement(await retrieval.Search("Green unrelated object", null))
            .GetProperty("itemIds").EnumerateArray());
        Assert.Equal(4, boundary.Calls); // two indexed descriptions and two text queries; no images
        Assert.True(await db.SearchGrants.AnyAsync(g => g.ItemId == blue && g.UserId == campus.Other));
        Assert.False(await db.SearchGrants.AnyAsync(g => g.ItemId == red && g.UserId == campus.Other));
    }
}
