using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class HostedImageQueryTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class SyntheticEncoder : HttpMessageHandler, IHttpClientFactory, IInferenceBudget
    {
        public int TextCalls { get; private set; }
        public int ImageCalls { get; private set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        public Task Reserve(string provider, CancellationToken cancellation) => Task.CompletedTask;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Assert.Equal(RemoteEmbeddings.Endpoint, request.RequestUri!.ToString());
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            var part = json.RootElement.GetProperty("content").GetProperty("parts")[0];
            int axis;
            if (part.TryGetProperty("text", out var text))
            {
                TextCalls++;
                axis = text.GetString()!.Contains("Blue", StringComparison.Ordinal) ? 0 : 1;
            }
            else
            {
                ImageCalls++;
                var data = part.GetProperty("inline_data");
                Assert.Equal("image/jpeg", data.GetProperty("mime_type").GetString());
                using var bitmap = SKBitmap.Decode(Convert.FromBase64String(data.GetProperty("data").GetString()!));
                var color = bitmap.GetPixel(0, 0);
                axis = color.Blue > color.Red && color.Blue > color.Green ? 0 : color.Red > color.Green ? 1 : 2;
            }
            var vector = new float[768];
            vector[axis] = 1;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { embedding = new { values = vector } }) };
        }
    }

    [DatabaseFact]
    public async Task Image_only_and_combined_queries_rank_current_vectors_without_empty_text_requests()
    {
        using var uploader = await campus.Login(campus.Student);
        async Task<Guid> Register(string description, SKColor color)
        {
            using var form = new MultipartFormDataContent {
                { new StringContent(description), "description" }, { new StringContent("Synthetic courtyard"), "place" },
                { new StringContent("2026-01-01"), "date" }, { new StringContent("Synthetic desk"), "storage" },
                { new ByteArrayContent(Picture(color, 4)), "photo", "test.png" }
            };
            return (await CampusFixture.Response(await uploader.PostAsync("/api/v1/found", form))).GetProperty("id").GetGuid();
        }
        var blue = await Register("Blue bottle", SKColors.Blue);
        var red = await Register("Red bag", SKColors.Red);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, campus.Other.ToString())], "test"));
        var access = new Access(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>(), scope.ServiceProvider.GetRequiredService<ReleasePolicy>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Search:Provider"] = "hosted", ["Search:ExternalProcessingApproved"] = "true",
            ["Search:GeminiExternalProcessingApproved"] = "true", ["Search:ExternalImagesApproved"] = "true",
            ["Search:GeminiExternalImagesApproved"] = "true", ["Search:GeminiProviderDataCollection"] = "allow",
            ["AI:GeminiFreeTierOnly"] = "true", ["AI:GeminiKey"] = "synthetic-test-boundary",
            ["Search:CalibrationApproval"] = "SYNTHETIC VECTORS ONLY - not quality evidence",
            ["Search:CalibrationRevision"] = "gemini-embedding-2-google-768-v1",
            ["Search:TextMinimumScore"] = "0.9", ["Search:ImageMinimumScore"] = "0.9", ["Search:MultimodalMinimumScore"] = "0.9"
        }).Build();
        using var encoder = new SyntheticEncoder();
        var retrieval = new RetrievalService(db, access, encoder, config, scope.ServiceProvider.GetRequiredService<PrivateMedia>(),
            new RemoteEmbeddings(encoder, config, encoder));
        await retrieval.Index(blue);
        await retrieval.Index(red);
        Assert.Equal(2, encoder.TextCalls);
        Assert.Equal(2, encoder.ImageCalls);

        using var bluePhoto = new MemoryStream(Picture(SKColors.Blue, 9));
        var blueFile = new FormFile(bluePhoto, 0, bluePhoto.Length, "photo", "blue.png");
        var result = JsonSerializer.SerializeToElement(await retrieval.Search(null, blueFile));
        Assert.Equal(blue, Assert.Single(result.GetProperty("itemIds").EnumerateArray()).GetGuid());
        Assert.Equal(2, encoder.TextCalls); // image-only query must not spend a text request
        Assert.Equal(3, encoder.ImageCalls);
        Assert.True(await db.SearchGrants.AnyAsync(g => g.UserId == campus.Other && g.ItemId == blue));
        Assert.False(await db.SearchGrants.AnyAsync(g => g.UserId == campus.Other && g.ItemId == red));

        using var redPhoto = new MemoryStream(Picture(SKColors.Red, 9));
        var combined = JsonSerializer.SerializeToElement(await retrieval.Search("Red bag query", new FormFile(redPhoto, 0, redPhoto.Length, "photo", "red.png")));
        Assert.Equal(red, Assert.Single(combined.GetProperty("itemIds").EnumerateArray()).GetGuid());
        Assert.Equal(3, encoder.TextCalls);
        Assert.Equal(4, encoder.ImageCalls);

        using var greenPhoto = new MemoryStream(Picture(SKColors.Green, 9));
        var noMatch = JsonSerializer.SerializeToElement(await retrieval.Search("", new FormFile(greenPhoto, 0, greenPhoto.Length, "photo", "green.png")));
        Assert.Empty(noMatch.GetProperty("itemIds").EnumerateArray());
        Assert.Equal(3, encoder.TextCalls);

        Assert.Equal("search_input_required", (await Assert.ThrowsAsync<RuleException>(() => retrieval.Search(" ", null))).Code);
        var callsBeforeInvalid = encoder.ImageCalls + encoder.TextCalls;
        using var corrupt = new MemoryStream([1, 2, 3]);
        Assert.Equal("invalid_upload", (await Assert.ThrowsAsync<RuleException>(() => retrieval.Search("Blue", new FormFile(corrupt, 0, 3, "photo", "bad.png")))).Code);
        Assert.Equal(callsBeforeInvalid, encoder.ImageCalls + encoder.TextCalls);

        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM smart_campus.\"Embeddings\" WHERE \"ItemId\"={blue} AND \"Kind\"='image'");
        Assert.Equal("search_index_pending", (await Assert.ThrowsAsync<RuleException>(() => retrieval.Search(null, blueFile))).Code);
        Assert.Equal(callsBeforeInvalid, encoder.ImageCalls + encoder.TextCalls);
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
