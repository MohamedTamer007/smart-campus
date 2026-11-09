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

public sealed class RetrievalService(CampusDb db, Access access, IHttpClientFactory clients, IConfiguration config, PrivateMedia media)
{
    public const string Model = "google/siglip2-base-patch16-224";
    public const string Revision = "75de2d55ec2d0b4efc50b3e9ad70dba96a7b2fa2";
    public static double? ApprovedThreshold(IConfiguration configuration)
    {
        var threshold = configuration.GetValue<double?>("Search:MinimumScore");
        return !string.IsNullOrWhiteSpace(configuration["Search:CalibrationApproval"]) && threshold is >= -1 and <= 1 && double.IsFinite(threshold.Value) ? threshold : null;
    }
    public async Task<object> Search(string description, IFormFile? photo)
    {
        var actor = await access.Current();
        Rules.Required(description);
        var threshold = ApprovedThreshold(config);
        if (threshold is null)
            throw new RuleException("search_calibration_required", 503);
        if (await db.Items.AnyAsync(f => f.State == "available" && f.UploaderId != actor.Id && f.CustodianId != actor.Id &&
            !(actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty)) &&
            !db.Jobs.Any(j => j.Kind == "index" && j.EntityId == f.Id && j.State == "completed")))
            throw new RuleException("search_index_pending", 503);
        var text = await EmbedText(description);
        EmbeddingResponse? image = null;
        if (photo is not null)
        {
            if (photo.Length is <= 0 or > 5242880) throw new RuleException("invalid_upload");
            using var bytes = new MemoryStream();
            await photo.CopyToAsync(bytes);
            image = await EmbedImage(bytes.ToArray());
            if (image.Dimension != text.Dimension) throw new RuleException("invalid_embedding", 503);
        }
        var candidates = await Candidates(actor, text, image, threshold.Value);
        foreach (var id in candidates) db.SearchGrants.Add(new() { UserId = actor.Id, ItemId = id, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) });
        await db.SaveChangesAsync();
        return new { itemIds = candidates, rankingVersion = "siglip2-v1", calibration = config["Search:CalibrationApproval"] };
    }

    public async Task Index(Guid id)
    {
        var found = await db.Items.FindAsync(id) ?? throw new RuleException("not_found", 404);
        var description = await EmbedText(found.Description);
        await Store(found.Id, found.Id, "description", description);
        var images = await db.Media.Where(m => m.ItemId == id).ToArrayAsync();
        foreach (var image in images) await Store(id, image.Id, "image", await EmbedImage(await media.Read(image)));
    }

    public Task<EmbeddingResponse> EmbedText(string text) => Embed("embed/text", JsonContent.Create(new { text = Rules.Required(text) }));
    private Task<EmbeddingResponse> EmbedImage(byte[] image) => Embed("embed/image", new ByteArrayContent(image));
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

    private Task Store(Guid id, Guid source, string kind, EmbeddingResponse embedding)
    {
        var vector = Vector(embedding.Vector);
        return db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO smart_campus.\"Embeddings\" (\"ItemId\",\"SourceId\",\"Kind\",\"Model\",\"Revision\",\"Dimension\",\"Vector\") VALUES ({id},{source},{kind},{embedding.Model},{embedding.Revision},{embedding.Dimension},CAST({vector} AS vector)) ON CONFLICT (\"SourceId\",\"Revision\") DO UPDATE SET \"Vector\"=EXCLUDED.\"Vector\",\"Dimension\"=EXCLUDED.\"Dimension\"");
    }

    private async Task<Guid[]> Candidates(Actor actor, EmbeddingResponse text, EmbeddingResponse? image, double threshold)
    {
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH ranked AS (
              SELECT e."ItemId", max(1 - (e."Vector" <=> CAST(@text AS vector))) FILTER (WHERE e."Kind"='image') AS image_text,
              max(1 - (e."Vector" <=> CAST(@text AS vector))) FILTER (WHERE e."Kind"='description') AS finder_text,
              max(1 - (e."Vector" <=> CAST(@image AS vector))) FILTER (WHERE e."Kind"='image') AS image_image
              FROM smart_campus."Embeddings" e JOIN smart_campus."Items" f ON f."Id"=e."ItemId"
              WHERE e."Revision"=@revision AND e."Dimension"=@dimension AND f."State"='available'
                AND f."CustodianId" IS DISTINCT FROM @user AND f."UploaderId"<>@user
                AND (f."CustodianDepartmentId" IS NULL OR NOT (f."CustodianDepartmentId"=ANY(@scopes)))
              GROUP BY e."ItemId"
            ), scored AS (SELECT "ItemId", CASE WHEN @hasimage THEN (0.7*image_text+0.3*finder_text+image_image)/2
              ELSE 0.7*image_text+0.3*finder_text END AS score FROM ranked)
            SELECT "ItemId" FROM scored WHERE score>=@threshold ORDER BY score DESC,"ItemId" LIMIT 5
            """;
        command.Parameters.AddWithValue("text", Vector(text.Vector));
        command.Parameters.AddWithValue("image", Vector((image ?? text).Vector));
        command.Parameters.AddWithValue("hasimage", image is not null);
        command.Parameters.AddWithValue("revision", Revision);
        command.Parameters.AddWithValue("dimension", text.Dimension);
        command.Parameters.AddWithValue("user", actor.Id);
        command.Parameters.AddWithValue("scopes", actor.Roles.Contains("department") ? actor.Scopes : Array.Empty<Guid>());
        command.Parameters.AddWithValue("threshold", threshold);
        var candidates = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) candidates.Add(reader.GetGuid(0));
        return candidates.ToArray();
    }
    private static string Vector(float[] vector) => "[" + string.Join(',', vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]";
}
