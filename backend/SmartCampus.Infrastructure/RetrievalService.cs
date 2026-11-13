using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed record EmbeddingResponse(string Model, string Revision, int Dimension, float[] Vector);

public sealed class RetrievalService(CampusDb db, Access access, IHttpClientFactory clients, IConfiguration config, PrivateMedia media, RemoteEmbeddings remote)
{
    public const string Model = "google/siglip2-base-patch16-224";
    public const string Revision = "75de2d55ec2d0b4efc50b3e9ad70dba96a7b2fa2";
    private bool Hosted => config["Search:Provider"] == "hosted";
    public static double? ApprovedThreshold(IConfiguration configuration) => ApprovedThreshold(configuration, hasImage: false);

    public static double? ApprovedThreshold(IConfiguration configuration, bool hasImage, bool hasText = true)
    {
        // An approval for historical SigLIP2 must never authorize a different
        // vector space or score threshold after migrating to hosted inference.
        var hosted = configuration["Search:Provider"] == "hosted";
        if (hosted && configuration["Search:CalibrationRevision"] != RemoteEmbeddings.Revision(configuration)) return null;
        if (string.IsNullOrWhiteSpace(configuration["Search:CalibrationApproval"])) return null;

        // Hosted text and image embeddings have materially different score
        // distributions, so one shared threshold cannot safely calibrate both.
        var key = hosted
            ? hasImage ? hasText ? "Search:MultimodalMinimumScore" : "Search:ImageMinimumScore" : "Search:TextMinimumScore"
            : "Search:MinimumScore";
        var threshold = configuration.GetValue<double?>(key);
        return threshold is >= -1 and <= 1 && double.IsFinite(threshold.Value) ? threshold : null;
    }

    public static bool CalibrationApproved(IConfiguration configuration)
    {
        if (configuration["Search:Provider"] == "hosted")
            return ApprovedThreshold(configuration, hasImage: false).HasValue &&
                ApprovedThreshold(configuration, hasImage: true).HasValue &&
                ApprovedThreshold(configuration, hasImage: true, hasText: false).HasValue;
        return ApprovedThreshold(configuration).HasValue;
    }
    public async Task<object> Search(string? description, IFormFile? photo)
    {
        var actor = await access.Current();
        var hasText = !string.IsNullOrWhiteSpace(description);
        if (hasText) description = Rules.Required(description, 2000);
        else if (photo is null) throw new RuleException("search_input_required");
        if (Hosted && !RemoteEmbeddings.Configured(config))
            throw new RuleException("search_provider_not_configured", 503);
        var threshold = ApprovedThreshold(config, photo is not null, hasText);
        if (threshold is null)
            throw new RuleException("search_calibration_required", 503);
        if (Hosted && await HasVisibleUnindexedItems(actor, photo is not null))
            throw new RuleException("search_index_pending", 503);
        EmbeddingResponse? image = null;
        if (photo is not null)
        {
            if (photo.Length is <= 0 or > CampusImageNormalizer.MaximumUploadBytes) throw new RuleException("invalid_upload");
            using var bytes = new MemoryStream();
            await photo.CopyToAsync(bytes);
            image = await EmbedImage(bytes.ToArray());
        }
        var text = hasText ? await EmbedText(description!) : null;
        if (image is not null && text is not null && image.Dimension != text.Dimension) throw new RuleException("invalid_embedding", 503);
        var candidates = await Candidates(actor, text, image, threshold.Value);
        if (candidates.Length > 0)
        {
            // Repeated searches should not grow an unbounded set of identical
            // temporary permissions for the same requester and item.
            var now = DateTimeOffset.UtcNow;
            var previous = await db.SearchGrants.Where(g => g.UserId == actor.Id && candidates.Contains(g.ItemId))
                .OrderByDescending(g => g.ExpiresAt).ToArrayAsync();
            var latestByItem = previous.GroupBy(g => g.ItemId).ToDictionary(group => group.Key, group => group.First());
            foreach (var id in candidates)
            {
                if (latestByItem.TryGetValue(id, out var existing)) existing.ExpiresAt = now.AddMinutes(15);
                else db.SearchGrants.Add(new() { UserId = actor.Id, ItemId = id, ExpiresAt = now.AddMinutes(15) });
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        }
        return new { itemIds = candidates, rankingVersion = Hosted ? RemoteEmbeddings.Revision(config) : "siglip2-v1", calibration = config["Search:CalibrationApproval"] };
    }

    public async Task Index(Guid id)
    {
        var found = await db.Items.FindAsync(id) ?? throw new RuleException("not_found", 404);
        if (Hosted && !RemoteEmbeddings.Configured(config))
            throw new RuleException("search_provider_not_configured", 503);
        var revision = Hosted ? RemoteEmbeddings.Revision(config) : Revision;
        // A worker lease can expire while the AppPool is idle or after a restart.
        // Preserve each completed embedding before advancing so a resumed job
        // never spends another provider request for a vector already persisted.
        if (!await HasStored(found.Id, revision))
        {
            var description = Hosted ? await remote.Passage(found.Description) : await EmbedText(found.Description);
            await Store(found.Id, found.Id, "description", description);
        }
        // A university must approve third-party transfer of private photos separately.
        // Descriptions may still be indexed after text-processing approval.
        if (Hosted && (!config.GetValue<bool>("Search:ExternalImagesApproved") ||
            !config.GetValue<bool>("Search:GeminiExternalImagesApproved"))) return;
        var images = await db.Media.Where(m => m.ItemId == id).ToArrayAsync();
        foreach (var image in images)
        {
            if (await HasStored(image.Id, revision)) continue;
            await Store(id, image.Id, "image", await EmbedImage(await media.Read(image)));
        }
    }

    private async Task<bool> HasStored(Guid sourceId, string revision)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM smart_campus.\"Embeddings\" WHERE \"SourceId\"=@source AND \"Revision\"=@revision AND \"Model\"=@model AND \"Dimension\">0 AND (@dimension=0 OR \"Dimension\"=@dimension))";
        command.Parameters.AddWithValue("source", sourceId);
        command.Parameters.AddWithValue("revision", revision);
        command.Parameters.AddWithValue("model", Hosted ? RemoteEmbeddings.Model : Model);
        command.Parameters.AddWithValue("dimension", Hosted ? RemoteEmbeddings.Dimension : 0);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    private async Task<bool> HasVisibleUnindexedItems(Actor actor, bool requireImage)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
              SELECT 1 FROM smart_campus."Items" f
              WHERE f."State"='available' AND f."UploaderId"<>@user AND f."CustodianId" IS DISTINCT FROM @user
                AND (NOT @isdepartment OR f."CustodianDepartmentId" IS NULL OR NOT (f."CustodianDepartmentId"=ANY(@scopes)))
                AND (NOT EXISTS (
                  SELECT 1 FROM smart_campus."Embeddings" e
                  WHERE e."ItemId"=f."Id" AND e."Kind"='description' AND e."Model"=@model
                    AND e."Revision"=@revision AND e."Dimension"=@dimension
                ) OR (@requireimage AND EXISTS (
                  SELECT 1 FROM smart_campus."Media" m WHERE m."ItemId"=f."Id" AND NOT EXISTS (
                    SELECT 1 FROM smart_campus."Embeddings" e WHERE e."SourceId"=m."Id" AND e."Kind"='image'
                      AND e."Model"=@model AND e."Revision"=@revision AND e."Dimension"=@dimension
                  )
                )))
            )
            """;
        command.Parameters.AddWithValue("user", actor.Id);
        command.Parameters.AddWithValue("isdepartment", actor.Roles.Contains("department"));
        command.Parameters.AddWithValue("scopes", actor.Roles.Contains("department") ? actor.Scopes : Array.Empty<Guid>());
        command.Parameters.AddWithValue("model", RemoteEmbeddings.Model);
        command.Parameters.AddWithValue("revision", RemoteEmbeddings.Revision(config));
        command.Parameters.AddWithValue("dimension", RemoteEmbeddings.Dimension);
        command.Parameters.AddWithValue("requireimage", requireImage);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public Task<EmbeddingResponse> EmbedText(string text) => Hosted ? remote.Query(text) : Embed("embed/text", JsonContent.Create(new { text = Rules.Required(text) }));
    private Task<EmbeddingResponse> EmbedImage(byte[] image) => Hosted ? remote.Image(image) : Embed("embed/image", new ByteArrayContent(image));
    private async Task<EmbeddingResponse> Embed(string route, HttpContent content)
    {
        using (content)
        using (var client = clients.CreateClient("worker"))
        {
            var endpoint = config["Search:WorkerUrl"] ?? "http://127.0.0.1:8123/";
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !uri.IsLoopback || uri.Scheme != "http") throw new RuleException("worker_unavailable", 503);
            client.BaseAddress = uri;
            client.DefaultRequestHeaders.Add("X-Worker-Key", config["Search:WorkerKey"] ?? "");
            using var response = await client.PostAsync(route, content);
            if (!response.IsSuccessStatusCode) throw new RuleException("worker_unavailable", 503);
            var embedding = await response.Content.ReadFromJsonAsync<EmbeddingResponse>() ?? throw new RuleException("invalid_embedding", 503);
            Validate(embedding);
            return embedding;
        }
    }

    public static void Validate(EmbeddingResponse embedding)
    {
        if (embedding.Model != Model || embedding.Revision != Revision || embedding.Dimension <= 0 ||
            embedding.Vector.Length != embedding.Dimension || embedding.Vector.Any(v => !float.IsFinite(v)) ||
            Math.Abs(embedding.Vector.Sum(v => (double)v * v) - 1) > 0.01) throw new RuleException("invalid_embedding", 503);
    }

    private async Task Store(Guid id, Guid source, string kind, EmbeddingResponse embedding)
    {
        if (Hosted) RemoteEmbeddings.Validate(embedding, config);
        else Validate(embedding);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        var vectorType = $"{await VectorSchema(connection)}.vector";
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO smart_campus."Embeddings" ("ItemId","SourceId","Kind","Model","Revision","Dimension","Vector")
            VALUES (@item,@source,@kind,@model,@revision,@dimension,CAST(@vector AS {vectorType}))
            ON CONFLICT ("SourceId","Revision") DO UPDATE SET "Kind"=EXCLUDED."Kind","Model"=EXCLUDED."Model",
              "Vector"=EXCLUDED."Vector","Dimension"=EXCLUDED."Dimension"
            """;
        command.Parameters.AddWithValue("item", id);
        command.Parameters.AddWithValue("source", source);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("model", embedding.Model);
        command.Parameters.AddWithValue("revision", embedding.Revision);
        command.Parameters.AddWithValue("dimension", embedding.Dimension);
        command.Parameters.AddWithValue("vector", Vector(embedding.Vector));
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Guid[]> Candidates(Actor actor, EmbeddingResponse? text, EmbeddingResponse? image, double threshold)
    {
        var query = text ?? image ?? throw new RuleException("search_input_required");
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        var vectorSchema = await VectorSchema(connection);
        var vectorType = $"{vectorSchema}.vector";
        var distanceOperator = $"OPERATOR({vectorSchema}.<=>)";
        command.CommandText = $"""
            WITH ranked AS (
              SELECT e."ItemId", max(1 - (e."Vector" {distanceOperator} CAST(@text AS {vectorType}))) FILTER (WHERE e."Kind"='image') AS image_text,
              max(1 - (e."Vector" {distanceOperator} CAST(@text AS {vectorType}))) FILTER (WHERE e."Kind"='description') AS finder_text,
              max(1 - (e."Vector" {distanceOperator} CAST(@image AS {vectorType}))) FILTER (WHERE e."Kind"='image') AS image_image
              FROM smart_campus."Embeddings" e JOIN smart_campus."Items" f ON f."Id"=e."ItemId"
              WHERE e."Model"=@model AND e."Revision"=@revision AND e."Dimension"=@dimension AND f."State"='available'
                AND f."CustodianId" IS DISTINCT FROM @user AND f."UploaderId"<>@user
                AND (f."CustodianDepartmentId" IS NULL OR NOT (f."CustodianDepartmentId"=ANY(@scopes)))
              GROUP BY e."ItemId"
            ), scored AS (SELECT "ItemId", CASE WHEN NOT @hastext THEN image_image WHEN @hasimage THEN
                (COALESCE(0.7*image_text+0.3*finder_text, image_text, finder_text)
                 + COALESCE(image_image, image_text, finder_text))/2
              ELSE COALESCE(0.7*image_text+0.3*finder_text, image_text, finder_text)
              END AS score FROM ranked)
            SELECT "ItemId" FROM scored WHERE score>=@threshold ORDER BY score DESC,"ItemId" LIMIT 5
            """;
        command.Parameters.AddWithValue("text", Vector(query.Vector));
        command.Parameters.AddWithValue("image", Vector((image ?? query).Vector));
        command.Parameters.AddWithValue("hastext", text is not null);
        command.Parameters.AddWithValue("hasimage", image is not null);
        command.Parameters.AddWithValue("model", Hosted ? RemoteEmbeddings.Model : Model);
        command.Parameters.AddWithValue("revision", Hosted ? RemoteEmbeddings.Revision(config) : Revision);
        command.Parameters.AddWithValue("dimension", query.Dimension);
        command.Parameters.AddWithValue("user", actor.Id);
        command.Parameters.AddWithValue("scopes", actor.Roles.Contains("department") ? actor.Scopes : Array.Empty<Guid>());
        command.Parameters.AddWithValue("threshold", threshold);
        var candidates = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) candidates.Add(reader.GetGuid(0));
        return candidates.ToArray();
    }

    private static async Task<string> VectorSchema(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT n.nspname FROM pg_extension e JOIN pg_namespace n ON n.oid=e.extnamespace WHERE e.extname='vector'";
        var schema = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(schema)) throw new RuleException("vector_extension_unavailable", 503);
        return new NpgsqlCommandBuilder().QuoteIdentifier(schema);
    }

    private static string Vector(float[] vector) => "[" + string.Join(',', vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
}
