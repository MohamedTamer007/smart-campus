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
using SmartCampus.Domain;
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
            Assert.Equal(RemoteEmbeddings.Endpoint, request.RequestUri!.ToString());
            using var input = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            var root = input.RootElement;
            Assert.Equal(768, root.GetProperty("outputDimensionality").GetInt32());
            Assert.Equal("synthetic-gemini-key", request.Headers.GetValues("x-goog-api-key").Single());
            var description = root.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            Assert.NotNull(description); // no image can leave this test process
            float[] vector = new float[RemoteEmbeddings.Dimension];
            vector[description.Contains("Blue", StringComparison.Ordinal) ? 0 :
                description.Contains("Red", StringComparison.Ordinal) ? 1 : 2] = 1;
            var response = new { embedding = new { values = vector } };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
        }
    }

    private sealed class NoOpBudget : IInferenceBudget
    {
        public Task Reserve(string provider, CancellationToken cancellation)
        {
            Assert.Equal("gemini", provider);
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
            ["Search:GeminiExternalProcessingApproved"] = "true",
            ["Search:ExternalImagesApproved"] = "true", // generic approval cannot substitute for the independent Google image approval
            ["Search:GeminiExternalImagesApproved"] = "false",
            ["Search:GeminiProviderDataCollection"] = "allow", // synthetic fixture only
            ["Search:TextMinimumScore"] = "0.9",
            ["Search:MultimodalMinimumScore"] = "0.9",
            ["Search:CalibrationApproval"] = "TEST ONLY — not a real score approval",
            ["Search:CalibrationRevision"] = "gemini-embedding-2-google-768-v1",
            ["AI:GeminiKey"] = "synthetic-gemini-key",
            ["AI:GeminiFreeTierOnly"] = "true",
            ["AI:GeminiDailyRequestLimit"] = "50",
            ["AI:GeminiRequestsPerMinute"] = "10"
        }).Build();

        using var boundary = new SyntheticBoundary();
        var remote = new RemoteEmbeddings(boundary, config, new NoOpBudget());
        var retrieval = new RetrievalService(db, access, boundary, config,
            scope.ServiceProvider.GetRequiredService<PrivateMedia>(), remote);
        await retrieval.Index(blue);
        await retrieval.Index(red);
        // A worker restart/requeue must reuse the persisted model revision.
        await retrieval.Index(blue);
        Assert.Equal(2, boundary.Calls);
        await db.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using (var schemaCommand = connection.CreateCommand())
        {
            schemaCommand.CommandText = "SELECT n.nspname FROM pg_extension e JOIN pg_namespace n ON n.oid=e.extnamespace WHERE e.extname='vector'";
            var extensionSchema = (string?)await schemaCommand.ExecuteScalarAsync();
            Assert.False(string.IsNullOrWhiteSpace(extensionSchema));
            var quotedSchema = new NpgsqlCommandBuilder().QuoteIdentifier(extensionSchema!);
            await using var corrupt = connection.CreateCommand();
            corrupt.CommandText = $"UPDATE smart_campus.\"Embeddings\" SET \"Dimension\"=512, \"Model\"='stale-model', \"Kind\"='image', \"Vector\"=CAST(@vector AS {quotedSchema}.vector) WHERE \"SourceId\"=@source AND \"Revision\"=@revision";
            corrupt.Parameters.AddWithValue("vector", "[" + string.Join(',', Enumerable.Range(0, 512).Select(i => i == 0 ? "1" : "0")) + "]");
            corrupt.Parameters.AddWithValue("source", blue);
            corrupt.Parameters.AddWithValue("revision", RemoteEmbeddings.Revision(config));
            Assert.Equal(1, await corrupt.ExecuteNonQueryAsync());
        }
        await retrieval.Index(blue);
        Assert.Equal(2, boundary.Calls); // mismatch is repaired from the bounded embedding cache, without another provider request
        Assert.Equal(RemoteEmbeddings.Dimension, await db.Database.SqlQuery<int>(
            $"SELECT \"Dimension\" AS \"Value\" FROM smart_campus.\"Embeddings\" WHERE \"SourceId\"={blue} AND \"Revision\"={RemoteEmbeddings.Revision(config)}").SingleAsync());
        Assert.Equal(RemoteEmbeddings.Model, await db.Database.SqlQuery<string>(
            $"SELECT \"Model\" AS \"Value\" FROM smart_campus.\"Embeddings\" WHERE \"SourceId\"={blue} AND \"Revision\"={RemoteEmbeddings.Revision(config)}").SingleAsync());
        Assert.Equal("description", await db.Database.SqlQuery<string>(
            $"SELECT \"Kind\" AS \"Value\" FROM smart_campus.\"Embeddings\" WHERE \"SourceId\"={blue} AND \"Revision\"={RemoteEmbeddings.Revision(config)}").SingleAsync());
        await db.Jobs.Where(j => j.Kind == "index" && (j.EntityId == blue || j.EntityId == red))
            .ExecuteUpdateAsync(update => update.SetProperty(j => j.State, "completed"));

        var response = JsonSerializer.SerializeToElement(await retrieval.Search("Blue distinctive bottle", null));
        Assert.Equal(blue, Assert.Single(response.GetProperty("itemIds").EnumerateArray()).GetGuid());
        Assert.Equal(RemoteEmbeddings.Revision(config), response.GetProperty("rankingVersion").GetString());
        Assert.True(response.GetProperty("calibrated").GetBoolean());
        Assert.Equal("TEST ONLY — not a real score approval", response.GetProperty("calibration").GetString());
        var noMatch = JsonSerializer.SerializeToElement(await retrieval.Search("Green unrelated object", null));
        Assert.Empty(noMatch.GetProperty("itemIds").EnumerateArray());
        Assert.True(noMatch.GetProperty("calibrated").GetBoolean());
        Assert.Equal("TEST ONLY — not a real score approval", noMatch.GetProperty("calibration").GetString());
        Assert.Equal(4, boundary.Calls); // two passages and two distinct text queries; no image request
        Assert.True(await db.SearchGrants.AnyAsync(g => g.ItemId == blue && g.UserId == campus.Other));
        Assert.False(await db.SearchGrants.AnyAsync(g => g.ItemId == red && g.UserId == campus.Other));

        await retrieval.Search("Blue distinctive bottle", null);
        Assert.Equal(1, await db.SearchGrants.CountAsync(g => g.ItemId == blue && g.UserId == campus.Other));
        var grant = await db.SearchGrants.SingleAsync(g => g.ItemId == blue && g.UserId == campus.Other);
        grant.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        await retrieval.Search("Blue distinctive bottle", null);
        Assert.Equal(1, await db.SearchGrants.CountAsync(g => g.ItemId == blue && g.UserId == campus.Other));
        Assert.True(grant.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(4, boundary.Calls); // cached query embeddings reuse the provider result; grants are still refreshed in place

        // Missing opt-in must preserve the default closed state and must stop before provider use.
        config["Search:CalibrationApproval"] = null;
        config["Search:AllowUncalibratedCandidates"] = null;
        var missingOptIn = await Assert.ThrowsAsync<RuleException>(() => retrieval.Search("Blue distinctive bottle", null));
        Assert.Equal("search_calibration_required", missingOptIn.Code);
        Assert.Equal(4, boundary.Calls);

        config["Search:AllowUncalibratedCandidates"] = "false";
        var disabledOptIn = await Assert.ThrowsAsync<RuleException>(() => retrieval.Search("Blue distinctive bottle", null));
        Assert.Equal("search_calibration_required", disabledOptIn.Code);
        Assert.Equal(4, boundary.Calls); // both disabled forms block before provider use

        // The synthetic vectors rank Blue above Red; exposing both here tests candidate ordering,
        // not retrieval precision or a production-quality threshold.
        config["Search:AllowUncalibratedCandidates"] = "true";
        var candidates = JsonSerializer.SerializeToElement(await retrieval.Search("Blue distinctive bottle", null));
        Assert.Equal(new[] { blue, red }, candidates.GetProperty("itemIds").EnumerateArray()
            .Select(value => value.GetGuid()).ToArray());
        Assert.False(candidates.GetProperty("calibrated").GetBoolean());
        Assert.Equal(JsonValueKind.Null, candidates.GetProperty("calibration").ValueKind);
        Assert.Equal(4, boundary.Calls); // the query embedding is cached; no automatic resend occurs

        // A valid reviewed threshold continues to filter candidates even while opt-in is enabled.
        config["Search:CalibrationApproval"] = "TEST ONLY — not a real score approval";
        var calibratedAgain = JsonSerializer.SerializeToElement(await retrieval.Search("Green unrelated object", null));
        Assert.Empty(calibratedAgain.GetProperty("itemIds").EnumerateArray());
        Assert.True(calibratedAgain.GetProperty("calibrated").GetBoolean());
        Assert.Equal("TEST ONLY — not a real score approval", calibratedAgain.GetProperty("calibration").GetString());
        Assert.Equal(4, boundary.Calls);
    }
}
