using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>
/// Stores one Gemini text vector per maintenance report so the dashboard can recognise
/// repeats worded differently. If embeddings are unavailable the dashboard falls back to
/// room-number and wording rules (see RecurrenceAnalysis).
/// </summary>
public sealed class ReportSimilarity(CampusDb db, RemoteEmbeddings remote, IConfiguration config)
{
    public static bool Enabled(IConfiguration configuration) =>
        configuration.GetValue("Maintenance:SimilarReports", true) && RemoteEmbeddings.Configured(configuration);

    public async Task Index(Guid reportId)
    {
        var report = await db.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId);
        if (report is null || report.CancelledAt.HasValue) return;
        if (!Enabled(config)) throw new RuleException("search_provider_not_configured", 503);
        var embedding = await remote.Passage(report.Description + " | " + report.Details);
        RemoteEmbeddings.Validate(embedding, config);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        var schema = await RetrievalService.VectorSchema(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO smart_campus."ReportEmbeddings" ("ReportId","ContentVersion","Revision","Vector")
            VALUES (@report,@version,@revision,CAST(@vector AS {schema}.vector))
            ON CONFLICT ("ReportId") DO UPDATE SET "ContentVersion"=EXCLUDED."ContentVersion","Revision"=EXCLUDED."Revision","Vector"=EXCLUDED."Vector"
            WHERE "ReportEmbeddings"."ContentVersion"<=EXCLUDED."ContentVersion"
            """;
        command.Parameters.AddWithValue("report", report.Id);
        command.Parameters.AddWithValue("version", report.ContentVersion);
        command.Parameters.AddWithValue("revision", embedding.Revision);
        command.Parameters.AddWithValue("vector", "[" + string.Join(',', embedding.Vector.Select(v => v.ToString("R", CultureInfo.InvariantCulture))) + "]");
        await command.ExecuteNonQueryAsync();
    }

    /// <summary>Stored vectors for the given reports; empty when the table is missing or embeddings were never built.</summary>
    public async Task<Dictionary<Guid, float[]>> Vectors(Guid[] reportIds)
    {
        var result = new Dictionary<Guid, float[]>();
        if (reportIds.Length == 0) return result;
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"ReportId\", \"Vector\"::text FROM smart_campus.\"ReportEmbeddings\" WHERE \"ReportId\"=ANY(@ids) AND \"Revision\"=@revision";
        command.Parameters.AddWithValue("ids", reportIds);
        command.Parameters.AddWithValue("revision", RemoteEmbeddings.Revision(config));
        try
        {
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result[reader.GetGuid(0)] = reader.GetString(1).Trim('[', ']').Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
        }
        catch (PostgresException) { result.Clear(); } // table not migrated yet: text rules only
        return result;
    }
}
