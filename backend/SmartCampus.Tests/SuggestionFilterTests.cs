using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class SuggestionFilterTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class SyntheticEncoder : HttpMessageHandler, IHttpClientFactory, IInferenceBudget
    {
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        public Task Reserve(string provider, CancellationToken cancellation) => Task.CompletedTask;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Assert.Equal(RemoteEmbeddings.Endpoint, request.RequestUri!.ToString());
            var vector = new float[RemoteEmbeddings.Dimension];
            vector[1] = 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = JsonContent.Create(new { embedding = new { values = vector } })
            });
        }
    }

    [DatabaseFact]
    public async Task Suggestions_keep_cross_photo_candidates_and_report_actual_scores_without_claiming_calibration()
    {
        // Regression: separate unreviewed floors hid an eligible watch photographed differently.
        using var uploader = await campus.Login(campus.Student);
        var watch = await campus.Item(uploader);
        var phone = await campus.Item(uploader);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        (await db.Items.FindAsync(watch))!.Description = "Red watch";
        // A neutral object: the relevance layer drops items of a *different named category*
        // (covered in SearchRelevanceTests), so this one stays a cross-photo candidate.
        (await db.Items.FindAsync(phone))!.Description = "Blue umbrella";
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, campus.Other.ToString())], "test"));
        var access = new Access(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>(), scope.ServiceProvider.GetRequiredService<ReleasePolicy>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Search:Provider"] = "hosted", ["Search:AllowUncalibratedCandidates"] = "true",
            ["Search:ExternalProcessingApproved"] = "true", ["Search:GeminiExternalProcessingApproved"] = "true",
            ["Search:ExternalImagesApproved"] = "true", ["Search:GeminiExternalImagesApproved"] = "true",
            ["Search:GeminiProviderDataCollection"] = "allow", ["AI:GeminiFreeTierOnly"] = "true",
            ["AI:GeminiKey"] = "synthetic-test-boundary"
        }).Build();
        using var encoder = new SyntheticEncoder();
        var retrieval = new RetrievalService(db, access, encoder, config, scope.ServiceProvider.GetRequiredService<PrivateMedia>(),
            new RemoteEmbeddings(encoder, config, encoder));
        await retrieval.Index(watch);
        await retrieval.Index(phone);

        // Controlled vectors simulate a different photo: neither modality reaches
        // the superseded 0.75/0.90 floors, but the watch is the closest candidate.
        await SetSimilarity(db, watch, "description", 0.60f);
        await SetSimilarity(db, watch, "image", 0.86f);
        await SetSimilarity(db, phone, "description", 0.55f);
        await SetSimilarity(db, phone, "image", 0.78f);
        using var photo = new MemoryStream(Picture(SKColors.Red, 9));
        var file = new FormFile(photo, 0, photo.Length, "photo", "synthetic-watch.png");
        var response = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", file), JsonOptions);
        Assert.Equal(new[] { watch, phone }, response.GetProperty("itemIds").EnumerateArray().Select(id => id.GetGuid()).ToArray());
        var candidates = response.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Equal(watch, candidates[0].GetProperty("itemId").GetGuid());
        Assert.Equal(0.821, candidates[0].GetProperty("score").GetDouble(), 5);
        Assert.Equal(0.782, candidates[0].GetProperty("textScore").GetDouble(), 5);
        Assert.Equal(0.86, candidates[0].GetProperty("imageScore").GetDouble(), 5);
        Assert.Equal(0.7455, candidates[1].GetProperty("score").GetDouble(), 5);
        Assert.False(response.GetProperty("calibrated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, response.GetProperty("calibration").ValueKind);
        Assert.Equal(2, await db.SearchGrants.CountAsync(g => g.UserId == campus.Other));

        var textOnly = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", null), JsonOptions);
        var firstText = textOnly.GetProperty("candidates")[0];
        Assert.Equal(watch, firstText.GetProperty("itemId").GetGuid());
        Assert.Equal(0.782, firstText.GetProperty("score").GetDouble(), 5);
        Assert.Equal(JsonValueKind.Null, firstText.GetProperty("imageScore").ValueKind);

        // The uploader's own inventory remains excluded; no search grants bypass custody.
        var ownerPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, campus.Student.ToString())], "test"));
        var ownerAccess = new Access(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = ownerPrincipal } },
            scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>(), scope.ServiceProvider.GetRequiredService<ReleasePolicy>());
        var ownerRetrieval = new RetrievalService(db, ownerAccess, encoder, config, scope.ServiceProvider.GetRequiredService<PrivateMedia>(),
            new RemoteEmbeddings(encoder, config, encoder));
        var ownerResponse = JsonSerializer.SerializeToElement(await ownerRetrieval.Search("Red watch query", file), JsonOptions);
        Assert.Empty(ownerResponse.GetProperty("itemIds").EnumerateArray());
        Assert.Empty(ownerResponse.GetProperty("candidates").EnumerateArray());
        Assert.False(await db.SearchGrants.AnyAsync(g => g.UserId == campus.Student));

        // Reviewed thresholds still filter the same overall score.
        config["Search:CalibrationApproval"] = "SYNTHETIC ONLY - not retrieval quality evidence";
        config["Search:CalibrationRevision"] = RemoteEmbeddings.Revision(config);
        config["Search:MultimodalMinimumScore"] = "0.8";
        var reviewed = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", file), JsonOptions);
        Assert.Equal(watch, Assert.Single(reviewed.GetProperty("itemIds").EnumerateArray()).GetGuid());
        Assert.True(reviewed.GetProperty("calibrated").GetBoolean());
        config["Search:MultimodalMinimumScore"] = "0.85";
        var rejected = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", file), JsonOptions);
        Assert.Empty(rejected.GetProperty("itemIds").EnumerateArray());
        Assert.Empty(rejected.GetProperty("candidates").EnumerateArray());
    }

    private static async Task SetSimilarity(CampusDb db, Guid item, string kind, float similarity)
    {
        var vector = new float[RemoteEmbeddings.Dimension];
        vector[1] = similarity;
        vector[3] = MathF.Sqrt(1 - similarity * similarity);
        var values = "[" + string.Join(',', vector.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]";
        await db.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT n.nspname FROM pg_extension e JOIN pg_namespace n ON n.oid=e.extnamespace WHERE e.extname='vector'";
        var schema = new NpgsqlCommandBuilder().QuoteIdentifier((string)(await command.ExecuteScalarAsync())!);
        command.CommandText = $"UPDATE smart_campus.\"Embeddings\" SET \"Vector\"=CAST(@vector AS {schema}.vector) WHERE \"ItemId\"=@item AND \"Kind\"=@kind";
        command.Parameters.AddWithValue("vector", values);
        command.Parameters.AddWithValue("item", item);
        command.Parameters.AddWithValue("kind", kind);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static byte[] Picture(SKColor color, int edge)
    {
        using var bitmap = new SKBitmap(edge, edge);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        return encoded!.ToArray();
    }
}
