using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Queues missing Gemini vectors only after an operator explicitly approves
/// reindexing. It is idempotent across process restarts and batch bounded.
/// </summary>
public sealed class GeminiEmbeddingReindexer(
    IServiceScopeFactory scopes,
    IConfiguration config,
    ILogger<GeminiEmbeddingReindexer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (config.GetValue<bool>("Search:GeminiReindexApproved") && RemoteEmbeddings.Configured(config))
            {
                try
                {
                    await QueueBatch(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning("Gemini reindex scheduling unavailable: {ExceptionType}", exception.GetType().Name);
                }
            }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    public async Task QueueBatch(CancellationToken cancellation)
    {
        if (!config.GetValue<bool>("Search:GeminiReindexApproved") || !RemoteEmbeddings.Configured(config)) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var revision = RemoteEmbeddings.Revision(config);
        var generation = Math.Clamp(config.GetValue("Search:GeminiReindexGeneration", 0), 0, 1_000_000);
        var keySuffix = generation == 0 ? "" : $"-retry-{generation}";
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellation);
        await using var query = connection.CreateCommand();
        query.CommandText = """
            SELECT item."Id" FROM smart_campus."Items" item
            WHERE item."State"='available' AND (
              NOT EXISTS (
                SELECT 1 FROM smart_campus."Embeddings" embedding
                WHERE embedding."ItemId"=item."Id" AND embedding."Kind"='description'
                  AND embedding."Model"=@model AND embedding."Revision"=@revision AND embedding."Dimension"=@dimension
              ) OR (
                @includeImages AND EXISTS (
                  SELECT 1 FROM smart_campus."Media" media
                  WHERE media."ItemId"=item."Id" AND NOT EXISTS (
                    SELECT 1 FROM smart_campus."Embeddings" embedding
                    WHERE embedding."SourceId"=media."Id" AND embedding."Kind"='image'
                      AND embedding."Model"=@model AND embedding."Revision"=@revision AND embedding."Dimension"=@dimension
                  )
                )
              )
            )
            AND NOT EXISTS (
              SELECT 1 FROM smart_campus."Jobs" job
              WHERE job."Key"=('index:' || item."Id"::text || ':gemini-embedding-2-768-v1' || @keySuffix)
            )
            AND NOT EXISTS (
              SELECT 1 FROM smart_campus."Jobs" job
              WHERE job."EntityId"=item."Id" AND job."Kind"='index' AND job."State" IN ('pending','running')
            )
            ORDER BY item."CreatedAt" LIMIT @batch
            """;
        query.Parameters.AddWithValue("model", RemoteEmbeddings.Model);
        query.Parameters.AddWithValue("revision", revision);
        query.Parameters.AddWithValue("dimension", RemoteEmbeddings.Dimension);
        query.Parameters.AddWithValue("keySuffix", keySuffix);
        query.Parameters.AddWithValue("includeImages", config.GetValue<bool>("Search:ExternalImagesApproved") &&
            config.GetValue<bool>("Search:GeminiExternalImagesApproved"));
        query.Parameters.AddWithValue("batch", Math.Clamp(config.GetValue("Search:GeminiReindexBatchSize", 25), 1, 100));
        var candidateList = new List<Guid>();
        await using (var reader = await query.ExecuteReaderAsync(cancellation))
            while (await reader.ReadAsync(cancellation)) candidateList.Add(reader.GetGuid(0));
        var candidates = candidateList.ToArray();
        if (candidates.Length == 0) return;

        var keys = candidates.Select(id => ReindexKey(id, generation)).ToArray();
        var alreadyQueued = await db.Jobs.AsNoTracking().Where(job => keys.Contains(job.Key))
            .Select(job => job.Key).ToArrayAsync(cancellation);
        foreach (var id in candidates)
        {
            var key = ReindexKey(id, generation);
            if (alreadyQueued.Contains(key, StringComparer.Ordinal)) continue;
            db.Jobs.Add(new BackgroundJob { Kind = "index", EntityId = id, InputVersion = 2, Key = key });
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellation);
    }

    private static string ReindexKey(Guid itemId, int generation) =>
        $"index:{itemId}:gemini-embedding-2-768-v1" + (generation == 0 ? "" : $"-retry-{generation}");
}
