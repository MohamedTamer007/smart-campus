using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class GeminiReindexerTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Reindex_queue_is_opt_in_bounded_and_idempotent()
    {
        using var student = await campus.Login(campus.Student);
        var itemId = await campus.Item(student);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var original = await db.Jobs.SingleAsync(job => job.EntityId == itemId && job.Kind == "index");
        original.State = "completed"; // simulate a previously indexed item whose old model vectors are stale
        await db.SaveChangesAsync();

        var services = campus.Services.GetRequiredService<IServiceScopeFactory>();
        var disabled = new GeminiEmbeddingReindexer(services, Config(reindex: false), NullLogger<GeminiEmbeddingReindexer>.Instance);
        await disabled.QueueBatch(CancellationToken.None);
        Assert.False(await db.Jobs.AnyAsync(job => job.Key == ReindexKey(itemId)));

        var enabled = new GeminiEmbeddingReindexer(services, Config(reindex: true, batchSize: 1), NullLogger<GeminiEmbeddingReindexer>.Instance);
        await enabled.QueueBatch(CancellationToken.None);
        await enabled.QueueBatch(CancellationToken.None);
        Assert.Equal(1, await db.Jobs.CountAsync(job => job.Key == ReindexKey(itemId)));
        Assert.Equal("pending", await db.Jobs.Where(job => job.Key == ReindexKey(itemId)).Select(job => job.State).SingleAsync());
    }

    [DatabaseFact]
    public async Task Reindex_queue_includes_items_with_current_text_but_missing_approved_image_vectors()
    {
        using var student = await campus.Login(campus.Student);
        var itemId = await campus.Item(student);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var original = await db.Jobs.SingleAsync(job => job.EntityId == itemId && job.Kind == "index");
        original.State = "completed";
        await db.SaveChangesAsync();

        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "SELECT n.nspname FROM pg_extension e JOIN pg_namespace n ON e.extnamespace=n.oid WHERE e.extname='vector'";
        var vectorSchema = new NpgsqlCommandBuilder().QuoteIdentifier((string)(await schemaCommand.ExecuteScalarAsync())!);
        await using var insert = connection.CreateCommand();
        insert.CommandText = $"""
            INSERT INTO smart_campus."Embeddings" ("ItemId","SourceId","Kind","Model","Revision","Dimension","Vector")
            VALUES (@item,@source,'description',@model,@revision,@dimension,CAST(@vector AS {vectorSchema}.vector))
            """;
        insert.Parameters.AddWithValue("item", itemId);
        insert.Parameters.AddWithValue("source", itemId);
        insert.Parameters.AddWithValue("model", RemoteEmbeddings.Model);
        insert.Parameters.AddWithValue("revision", RemoteEmbeddings.Revision(Config(reindex: true)));
        insert.Parameters.AddWithValue("dimension", RemoteEmbeddings.Dimension);
        insert.Parameters.AddWithValue("vector", "[" + string.Join(',', Enumerable.Repeat("0", RemoteEmbeddings.Dimension - 1).Prepend("1")) + "]");
        await insert.ExecuteNonQueryAsync();

        var serviceFactory = campus.Services.GetRequiredService<IServiceScopeFactory>();
        var withoutImageApproval = new GeminiEmbeddingReindexer(serviceFactory, Config(reindex: true, includeImages: false),
            NullLogger<GeminiEmbeddingReindexer>.Instance);
        await withoutImageApproval.QueueBatch(CancellationToken.None);
        Assert.False(await db.Jobs.AnyAsync(job => job.Key == ReindexKey(itemId)));

        var reindexer = new GeminiEmbeddingReindexer(serviceFactory, Config(reindex: true, includeImages: true),
            NullLogger<GeminiEmbeddingReindexer>.Instance);
        await reindexer.QueueBatch(CancellationToken.None);

        Assert.Equal(1, await db.Jobs.CountAsync(job => job.Key == ReindexKey(itemId)));
        Assert.Equal("pending", await db.Jobs.Where(job => job.Key == ReindexKey(itemId)).Select(job => job.State).SingleAsync());
    }

    [DatabaseFact]
    public async Task Terminal_reindex_failures_are_retried_only_after_an_explicit_generation_change()
    {
        using var student = await campus.Login(campus.Student);
        var itemId = await campus.Item(student);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var original = await db.Jobs.SingleAsync(job => job.EntityId == itemId && job.Kind == "index");
        original.State = "completed";
        await db.SaveChangesAsync();

        var services = campus.Services.GetRequiredService<IServiceScopeFactory>();
        var firstGeneration = new GeminiEmbeddingReindexer(services, Config(reindex: true), NullLogger<GeminiEmbeddingReindexer>.Instance);
        await firstGeneration.QueueBatch(CancellationToken.None);
        var failed = await db.Jobs.SingleAsync(job => job.Key == ReindexKey(itemId));
        failed.State = "unavailable";
        failed.Attempts = 3;
        await db.SaveChangesAsync();

        await firstGeneration.QueueBatch(CancellationToken.None);
        Assert.Equal(1, await db.Jobs.CountAsync(job => job.Key == ReindexKey(itemId)));
        Assert.False(await db.Jobs.AnyAsync(job => job.Key == ReindexKey(itemId, 1)));

        var explicitlyRetried = new GeminiEmbeddingReindexer(services, Config(reindex: true, generation: 1),
            NullLogger<GeminiEmbeddingReindexer>.Instance);
        await explicitlyRetried.QueueBatch(CancellationToken.None);
        await explicitlyRetried.QueueBatch(CancellationToken.None);

        Assert.Equal(1, await db.Jobs.CountAsync(job => job.Key == ReindexKey(itemId, 1)));
        Assert.Equal("unavailable", await db.Jobs.Where(job => job.Key == ReindexKey(itemId)).Select(job => job.State).SingleAsync());
        Assert.Equal("pending", await db.Jobs.Where(job => job.Key == ReindexKey(itemId, 1)).Select(job => job.State).SingleAsync());
    }

    private static IConfiguration Config(bool reindex, int batchSize = 25, bool includeImages = false, int generation = 0) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Search:Provider"] = "hosted",
        ["Search:ExternalProcessingApproved"] = "true",
        ["Search:ExternalImagesApproved"] = includeImages.ToString(),
        ["Search:GeminiExternalProcessingApproved"] = "true",
        ["Search:GeminiExternalImagesApproved"] = includeImages.ToString(),
        ["Search:GeminiProviderDataCollection"] = "allow",
        ["Search:GeminiReindexApproved"] = reindex.ToString(),
        ["Search:GeminiReindexBatchSize"] = batchSize.ToString(),
        ["Search:GeminiReindexGeneration"] = generation.ToString(),
        ["AI:GeminiKey"] = "synthetic-test-key",
        ["AI:GeminiFreeTierOnly"] = "true",
        ["AI:GeminiDailyRequestLimit"] = "50",
        ["AI:GeminiRequestsPerMinute"] = "10"
    }).Build();

    private static string ReindexKey(Guid itemId, int generation = 0) =>
        $"index:{itemId}:gemini-embedding-2-768-v1" + (generation == 0 ? "" : $"-retry-{generation}");
}
