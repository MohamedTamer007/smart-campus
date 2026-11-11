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

public sealed class RetrievalTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class EncoderBoundary : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var text = request.RequestUri!.AbsolutePath.EndsWith("text", StringComparison.Ordinal)
                ? await request.Content!.ReadAsStringAsync(cancellationToken) : "Blue";
            float[] vector = text.Contains("Blue", StringComparison.Ordinal) ? [1, 0] : text.Contains("Red", StringComparison.Ordinal) ? [0, 1] : [0.70710677f, 0.70710677f];
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new EmbeddingResponse(RetrievalService.Model, RetrievalService.Revision, 2, vector)) };
        }
    }

    private sealed class NoInferenceBudget : IInferenceBudget
    {
        public Task Reserve(string provider, CancellationToken cancellation) => Task.CompletedTask;
    }

    [DatabaseFact]
    public async Task Pgvector_ranking_filters_candidates_and_persists_user_specific_result_grants()
    {
        using var uploader = await campus.Login(campus.Student);
        var blueId = await campus.Item(uploader);
        var redId = await campus.Item(uploader);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        (await db.Items.FindAsync(blueId))!.Description = "Blue distinctive bottle";
        (await db.Items.FindAsync(redId))!.Description = "Red bag";
        await db.SaveChangesAsync();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, campus.Other.ToString())], "test-boundary"));
        var access = new Access(db, new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>(), scope.ServiceProvider.GetRequiredService<ReleasePolicy>());
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Search:CalibrationApproval"] = "TEST ONLY - synthetic vectors", ["Search:MinimumScore"] = "0.9"
        }).Build();
        using var encoder = new EncoderBoundary();
        var retrieval = new RetrievalService(db, access, encoder, config, scope.ServiceProvider.GetRequiredService<PrivateMedia>(),
            new RemoteEmbeddings(encoder, config, new NoInferenceBudget()));
        await retrieval.Index(blueId);
        await retrieval.Index(redId);
        await db.Jobs.Where(j => j.Kind == "index" && (j.EntityId == blueId || j.EntityId == redId)).ExecuteUpdateAsync(update => update.SetProperty(j => j.State, "completed"));
        var response = JsonSerializer.SerializeToElement(await retrieval.Search("Blue distinctive bottle", null));
        Assert.Equal(blueId, Assert.Single(response.GetProperty("itemIds").EnumerateArray()).GetGuid());
        Assert.True(await db.SearchGrants.AnyAsync(g => g.ItemId == blueId && g.UserId == campus.Other && g.ExpiresAt > DateTimeOffset.UtcNow));
        Assert.False(await db.SearchGrants.AnyAsync(g => g.ItemId == redId && g.UserId == campus.Other));
        var empty = JsonSerializer.SerializeToElement(await retrieval.Search("Green unrelated object", null));
        Assert.Empty(empty.GetProperty("itemIds").EnumerateArray());
    }
}
