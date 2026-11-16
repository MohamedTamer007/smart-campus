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
    public async Task Suggestions_require_independent_text_and_photo_evidence_and_only_grant_accepted_items()
    {
        // Regression: a watch query previously included unrelated inventory such as phones.
        using var uploader = await campus.Login(campus.Student);
        var watch = await campus.Item(uploader);
        var phone = await campus.Item(uploader);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        (await db.Items.FindAsync(watch))!.Description = "Red watch";
        (await db.Items.FindAsync(phone))!.Description = "Blue phone";
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

        // The query's text and red image both use axis 1. These controlled cosine
        // scores reproduce high image similarity without corresponding text evidence.
        await SetSimilarity(db, watch, "description", 0.85f);
        await SetSimilarity(db, watch, "image", 0.95f);
        await SetSimilarity(db, phone, "description", 0.65f);
        await SetSimilarity(db, phone, "image", 0.99f);
        using var photo = new MemoryStream(Picture(SKColors.Red, 9));
        var file = new FormFile(photo, 0, photo.Length, "photo", "synthetic-watch.png");
        var response = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", file));
        Assert.Equal(watch, Assert.Single(response.GetProperty("itemIds").EnumerateArray()).GetGuid());
        Assert.False(response.GetProperty("calibrated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, response.GetProperty("calibration").ValueKind);
        Assert.True(await db.SearchGrants.AnyAsync(g => g.ItemId == watch && g.UserId == campus.Other));
        Assert.False(await db.SearchGrants.AnyAsync(g => g.ItemId == phone && g.UserId == campus.Other));

        // Passing text cannot compensate for a weak photo, either.
        await SetSimilarity(db, watch, "image", 0.85f);
        var rejectedPhoto = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", file));
        Assert.Empty(rejectedPhoto.GetProperty("itemIds").EnumerateArray());
        var textOnly = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", null));
        Assert.Equal(watch, Assert.Single(textOnly.GetProperty("itemIds").EnumerateArray()).GetGuid());

        // A below-floor description must also produce an honest empty result.
        await SetSimilarity(db, watch, "description", 0.74f);
        var absent = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", null));
        Assert.Empty(absent.GetProperty("itemIds").EnumerateArray());
        await SetSimilarity(db, watch, "description", 0.75f);
        var boundary = JsonSerializer.SerializeToElement(await retrieval.Search("Red watch query", null));
        Assert.Equal(watch, Assert.Single(boundary.GetProperty("itemIds").EnumerateArray()).GetGuid());

        foreach (var key in new[] { "Search:SuggestionTextMinimumScore", "Search:SuggestionImageMinimumScore" })
        {
            foreach (var invalid in new[] { "NaN", "Infinity", "0", "-0.1", "1.1", "invalid" })
            {
                config[key] = invalid;
                var error = await Assert.ThrowsAsync<RuleException>(() => retrieval.Search("Red watch query", file));
                Assert.Equal("search_filter_not_configured", error.Code);
                Assert.Equal(503, error.Status);
            }
            config[key] = null;
        }
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
