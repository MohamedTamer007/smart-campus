using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

[ApiController, Authorize, Route("api/v1/admin/ai-evaluations")]
public sealed class AiEvaluationController(CampusDb db, Access access, IConfiguration config,
    IHttpClientFactory clients, IInferenceBudget budget, TimeProvider clock) : ControllerBase
{
    private const string SuiteVersion = "2026-11-13-gemini-embedding-2-v1";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] Priorities = ["low", "medium", "high"];
    private static readonly string[] FaultTypes = ["plumbing", "electrical", "other"];

    [HttpGet]
    public async Task<ActionResult<EvaluationSnapshot>> Snapshot([FromQuery] Guid? runId, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        await ExpireInterruptedRuns(cancellation);
        var runs = await db.AiEvaluationRuns.OrderByDescending(x => x.CreatedAt)
            .Select(x => new EvaluationRunSummary(x.Id, x.Suite, x.SuiteVersion, x.State, x.CreatedAt, x.UpdatedAt))
            .Take(30).ToArrayAsync(cancellation);
        var latestSuite = runs.FirstOrDefault()?.Suite;
        var usage = await CurrentUsage(cancellation, latestSuite == "lost-found" ? "gemini" : "openrouter");
        var selectedRunId = runId ?? runs.FirstOrDefault()?.Id;
        var cases = selectedRunId.HasValue && runs.Any(x => x.Id == selectedRunId.Value)
            ? await db.AiEvaluationCases.Where(x => x.RunId == selectedRunId.Value).OrderBy(x => x.CaseKey).ToArrayAsync(cancellation)
            : [];
        return new EvaluationSnapshot(usage, runs, cases.Select(ToResponse).ToArray(), History());
    }

    [HttpGet("preflight")]
    public async Task<ActionResult<EvaluationPreflight>> Preflight([FromQuery] string suite, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        var specifications = Specifications(suite);
        var usage = await CurrentUsage(cancellation, suite == "lost-found" ? "gemini" : "openrouter");
        var calls = await CallsRequired(specifications, cancellation);
        return new EvaluationPreflight(suite, SuiteVersion, specifications.Length, calls, usage,
            calls == 0 ? "cached_embeddings" : usage.DailyRemaining == 0 || usage.MinuteRemaining == 0 ? "quota_blocked" : "ready");
    }

    [HttpPost("runs")]
    public async Task<ActionResult<EvaluationRunCreated>> CreateRun(CreateEvaluationRun request, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        var specifications = Specifications(request.Suite);
        await ExpireInterruptedRuns(cancellation);
        if (await db.AiEvaluationRuns.AnyAsync(x => x.IsActive, cancellation))
            throw new RuleException("ai_evaluation_run_active", 409);
        var usage = await CurrentUsage(cancellation, request.Suite == "lost-found" ? "gemini" : "openrouter");
        var callsRequired = await CallsRequired(specifications, cancellation);
        if (callsRequired > 0 && (usage.DailyRemaining == 0 || usage.MinuteRemaining == 0))
            throw new RuleException("ai_quota_exhausted", 429, usage.NextMinuteAt);
        var run = new AiEvaluationRun { Id = Guid.NewGuid(), Suite = request.Suite, SuiteVersion = SuiteVersion,
            State = "pending", IsActive = true, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow() };
        foreach (var spec in specifications)
        {
            var input = spec.Input with { ImageDataUrl = spec.Input.ImageDataUrl is null ? null : "data:image/png;base64," + spec.Input.ImageDataUrl };
            run.Cases.Add(new AiEvaluationCase { Id = Guid.NewGuid(), RunId = run.Id, CaseKey = spec.CaseKey, Kind = spec.Kind,
                InputJson = JsonSerializer.Serialize(input, JsonOptions), ExpectedJson = JsonSerializer.Serialize(spec.Expected, JsonOptions),
                EmbeddingCacheKey = spec.CacheKey });
        }
        db.AiEvaluationRuns.Add(run);
        await db.SaveChangesAsync(cancellation);
        return new EvaluationRunCreated(run.Id, run.Suite, specifications.Length, callsRequired, usage, SuiteVersion);
    }

    [HttpPost("runs/{runId:guid}/next")]
    public async Task<ActionResult<EvaluationStepResponse>> ExecuteNext(Guid runId, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        var run = await ClaimRun(runId, cancellation);
        var step = await ClaimNextCase(run, cancellation);
        var provider = run.Suite == "lost-found" ? "gemini" : "openrouter";
        if (step is null) return Ok(new EvaluationStepResponse(run.Id, run.State, null, await CurrentUsage(cancellation, provider)));
        try
        {
            var output = await ExecuteCase(run.Id, step, cancellation);
            step.State = output.Cached ? "cached" : "completed";
            step.ActualJson = output.Json;
            step.Model = output.Model;
            step.ElapsedMilliseconds = output.ElapsedMilliseconds;
            step.FinishedAt = clock.GetUtcNow();
        }
        catch (RuleException error) when (error.Code == "ai_quota_exhausted")
        {
            step.State = "pending";
            step.StartedAt = null;
            run.State = "paused";
            run.ActiveUntil = null;
            run.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellation);
            return Ok(new EvaluationStepResponse(run.Id, run.State, ToResponse(step), await CurrentUsage(cancellation, provider), "quota"));
        }
        catch (RuleException error)
        {
            step.State = "failed";
            step.ErrorCode = error.Code;
            step.FinishedAt = clock.GetUtcNow();
            run.State = "paused";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            step.State = "interrupted";
            step.ErrorCode = "request_interrupted_result_unknown";
            step.FinishedAt = clock.GetUtcNow();
            run.State = "paused";
            run.ActiveUntil = null;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        run.ActiveUntil = null;
        run.UpdatedAt = clock.GetUtcNow();
        if (!await db.AiEvaluationCases.AnyAsync(x => x.RunId == run.Id && x.State == "pending", cancellation))
        {
            run.State = "completed";
            run.IsActive = false;
        }
        await db.SaveChangesAsync(cancellation);
        return Ok(new EvaluationStepResponse(run.Id, run.State, ToResponse(step), await CurrentUsage(cancellation, provider)));
    }

    [HttpPost("runs/{runId:guid}/pause")]
    public async Task<ActionResult<EvaluationRunSummary>> Pause(Guid runId, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        var run = await db.AiEvaluationRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellation)
            ?? throw new RuleException("not_found", 404);
        if (run.ActiveUntil > clock.GetUtcNow()) throw new RuleException("ai_evaluation_step_active", 409);
        run.State = "paused";
        run.ActiveUntil = null;
        run.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellation);
        return Ok(ToSummary(run));
    }

    [HttpPost("runs/{runId:guid}/resume")]
    public async Task<ActionResult<EvaluationRunSummary>> Resume(Guid runId, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        var run = await db.AiEvaluationRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellation)
            ?? throw new RuleException("not_found", 404);
        if (run.State is not ("paused" or "pending")) throw new RuleException("ai_evaluation_run_not_paused", 409);
        run.State = "running";
        run.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellation);
        return Ok(ToSummary(run));
    }

    [HttpGet("runs/{runId:guid}")]
    public async Task<ActionResult<EvaluationRunDetail>> Detail(Guid runId, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        await ExpireInterruptedRuns(cancellation);
        var run = await db.AiEvaluationRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellation)
            ?? throw new RuleException("not_found", 404);
        var cases = await db.AiEvaluationCases.Where(x => x.RunId == runId).OrderBy(x => x.CaseKey).ToArrayAsync(cancellation);
        return new EvaluationRunDetail(ToSummary(run), cases.Select(ToResponse).ToArray());
    }

    [HttpGet("runs/{runId:guid}/csv")]
    public async Task<IActionResult> Csv(Guid runId, CancellationToken cancellation)
    {
        await RequireAdmin(cancellation);
        var run = await db.AiEvaluationRuns.SingleOrDefaultAsync(x => x.Id == runId, cancellation)
            ?? throw new RuleException("not_found", 404);
        var cases = await db.AiEvaluationCases.Where(x => x.RunId == runId).OrderBy(x => x.CaseKey).ToArrayAsync(cancellation);
        var csv = new StringBuilder("RecordType,Date,Source,Metric,Value,Notes,Case,Kind,State,Input,Expected,Actual,Model,ElapsedMs,Error\r\n");
        void Add(params string[] values) => csv.AppendJoin(',', values.Select(Csv)).Append("\r\n");
        Add("run", run.CreatedAt.ToString("O", CultureInfo.InvariantCulture), run.Suite, "suite state", run.State,
            run.SuiteVersion, "", "", "", "", "", "", "", "", "");
        Add("run", run.CreatedAt.ToString("O", CultureInfo.InvariantCulture), run.Suite, "total cases", cases.Length.ToString(CultureInfo.InvariantCulture),
            "", "", "", "", "", "", "", "", "", "");
        Add("run", run.CreatedAt.ToString("O", CultureInfo.InvariantCulture), run.Suite, "API passed", cases.Count(x => x.State is "completed" or "cached").ToString(CultureInfo.InvariantCulture),
            "", "", "", "", "", "", "", "", "", "");
        Add("run", run.CreatedAt.ToString("O", CultureInfo.InvariantCulture), run.Suite, "failed or unresolved", cases.Count(x => x.State is "failed" or "interrupted").ToString(CultureInfo.InvariantCulture),
            "", "", "", "", "", "", "", "", "", "");
        foreach (var field in new[] { "type", "priority", "risk" })
        {
            var (count, agreed) = ReferenceAgreement(cases, field);
            Add("measured-metric", "", run.Suite, field + " agreement with provisional reference",
                count == 0 ? "Not Evaluated" : $"{agreed}/{count} ({agreed * 100d / count:F1}%)",
                "Not general accuracy", "", "", "", "", "", "", "", "", "");
        }
        var positive = cases.Where(x => !string.IsNullOrWhiteSpace(JsonText(x.ExpectedJson, "itemId")) && x.ActualJson is not null).ToArray();
        foreach (var field in new[] { "top1", "top5" })
        {
            var measured = positive.Where(x => JsonBool(x.ActualJson!, field).HasValue).ToArray();
            var matches = measured.Count(x => JsonBool(x.ActualJson!, field) == true);
            Add("measured-metric", "", run.Suite, "positive retrieval " + field,
                measured.Length == 0 ? "Not Evaluated" : $"{matches}/{measured.Length} ({matches * 100d / measured.Length:F1}%)",
                "Positive cases only; no-match threshold is not evaluated", "", "", "", "", "", "", "", "", "");
        }
        var timed = cases.Where(x => x.ElapsedMilliseconds.HasValue).ToArray();
        Add("measured-metric", "", run.Suite, "mean request duration ms",
            timed.Length == 0 ? "Not Evaluated" : timed.Average(x => x.ElapsedMilliseconds!.Value).ToString("F1", CultureInfo.InvariantCulture),
            "", "", "", "", "", "", "", "", "", "");
        foreach (var source in History())
            for (var index = 0; index < source.ChartLabels.Length; index++)
                Add("historical", source.Date, source.Source, source.ChartLabels[index], source.ChartValues[index].ToString(CultureInfo.InvariantCulture),
                    source.Metrics + " " + source.Limits, "", "", "", "", "", "", "", "", "");
        foreach (var row in cases)
            Add("case", row.StartedAt?.ToString("O", CultureInfo.InvariantCulture) ?? "", run.Suite, "", "", "", row.CaseKey, row.Kind, row.State,
                row.InputJson, row.ExpectedJson, row.ActualJson ?? "", row.Model ?? "",
                row.ElapsedMilliseconds?.ToString(CultureInfo.InvariantCulture) ?? "", row.ErrorCode ?? "");
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"ai-evaluation-{run.Suite}-{run.CreatedAt:yyyyMMdd-HHmm}.csv");
    }

    private async Task RequireAdmin(CancellationToken cancellation) => Access.Require((await access.Current()).Admin);

    private async Task ExpireInterruptedRuns(CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var expired = await db.AiEvaluationRuns.Where(x => x.State == "running" && x.ActiveUntil < now).ToArrayAsync(cancellation);
        foreach (var run in expired)
        {
            var inFlight = await db.AiEvaluationCases.Where(x => x.RunId == run.Id && x.State == "running").ToArrayAsync(cancellation);
            foreach (var row in inFlight)
            {
                row.State = "interrupted";
                row.ErrorCode = "process_interrupted_result_unknown";
                row.FinishedAt = now;
            }
            run.State = "paused";
            run.ActiveUntil = null;
            run.UpdatedAt = now;
        }
        if (expired.Length > 0) await db.SaveChangesAsync(cancellation);
    }

    private async Task<AiEvaluationRun> ClaimRun(Guid id, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var changed = await db.AiEvaluationRuns.Where(x => x.Id == id &&
                (x.State == "pending" || x.State == "running") && (x.ActiveUntil == null || x.ActiveUntil < now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.State, "running")
                .SetProperty(x => x.ActiveUntil, now.AddMinutes(2)).SetProperty(x => x.UpdatedAt, now), cancellation);
        if (changed != 1) throw new RuleException("ai_evaluation_run_busy_or_not_runnable", 409);
        return await db.AiEvaluationRuns.Include(x => x.Cases).SingleAsync(x => x.Id == id, cancellation);
    }

    private async Task<AiEvaluationCase?> ClaimNextCase(AiEvaluationRun run, CancellationToken cancellation)
    {
        var row = await db.AiEvaluationCases.Where(x => x.RunId == run.Id && x.State == "pending")
            .OrderBy(x => x.CaseKey).FirstOrDefaultAsync(cancellation);
        if (row is null)
        {
            run.State = "completed";
            run.IsActive = false;
            run.ActiveUntil = null;
            run.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(cancellation);
            return null;
        }
        row.State = "running";
        row.StartedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(cancellation);
        return row;
    }

    private async Task<StepOutput> ExecuteCase(Guid runId, AiEvaluationCase row, CancellationToken cancellation)
    {
        var timer = Stopwatch.StartNew();
        var input = JsonSerializer.Deserialize<EvaluationInput>(row.InputJson, JsonOptions)!;
        if (row.Kind.StartsWith("maintenance", StringComparison.Ordinal))
        {
            var maintenanceSettings = OpenRouterSettings();
            var bytes = input.ImageDataUrl is null ? null : ImageBytes(input.ImageDataUrl);
            var suggestion = await new AiProviders(clients, maintenanceSettings, budget).Assess(input.Text!, cancellation, bytes);
            timer.Stop();
            return StepOutput.Create(new { suggestion.Type, suggestion.Priority, suggestion.Risk, suggestion.Explanation },
                AiProviders.SelectedModel(maintenanceSettings, bytes is not null), timer.ElapsedMilliseconds);
        }
        var settings = GeminiEmbeddingSettings();
        var key = row.EmbeddingCacheKey!;
        var revision = RemoteEmbeddings.Revision(settings);
        var cached = await db.AiEvaluationVectors.SingleOrDefaultAsync(x => x.CacheKey == key && x.Revision == revision, cancellation);
        EmbeddingResponse embedding;
        if (cached is null)
        {
            var embeddings = new RemoteEmbeddings(clients, settings, budget);
            embedding = row.Kind switch
            {
                "embedding-image" or "embedding-passage-image" or "embedding-query-image" =>
                    await embeddings.Image(ImageBytes(input.ImageDataUrl!), cancellation),
                "embedding-passage-text" => await embeddings.Passage(input.Text!, cancellation),
                _ => await embeddings.Query(input.Text!, cancellation)
            };
            db.AiEvaluationVectors.Add(new AiEvaluationVector { CacheKey = key, Revision = embedding.Revision, Dimension = embedding.Dimension,
                VectorJson = JsonSerializer.Serialize(embedding.Vector, JsonOptions), CreatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(cancellation);
        }
        else embedding = new EmbeddingResponse(RemoteEmbeddings.Model, cached.Revision, cached.Dimension,
            JsonSerializer.Deserialize<float[]>(cached.VectorJson, JsonOptions)!);
        timer.Stop();
        object actual = new { embedding.Model, embedding.Revision, embedding.Dimension, Source = cached is null ? "Google Gemini API" : "previous saved API embedding" };
        if (row.Kind.StartsWith("embedding-query", StringComparison.Ordinal))
        {
            var expected = JsonSerializer.Deserialize<EvaluationExpected>(row.ExpectedJson, JsonOptions)!;
            var candidates = await Rank(runId, input.Modality!, embedding.Vector, revision, cancellation);
            actual = new { embedding.Model, embedding.Revision, embedding.Dimension, Source = cached is null ? "Google Gemini API" : "previous saved API embedding",
                Candidates = candidates,
                Top1 = expected.ItemId is null ? (bool?)null : candidates.FirstOrDefault()?.ItemId == expected.ItemId,
                Top5 = expected.ItemId is null ? (bool?)null : candidates.Take(5).Any(x => x.ItemId == expected.ItemId) };
        }
        return StepOutput.Create(actual, RemoteEmbeddings.Model, timer.ElapsedMilliseconds, cached is not null);
    }

    private IConfiguration GeminiEmbeddingSettings()
    {
        var apiKey = config["AI:GeminiKey"];
        var values = new Dictionary<string, string?> {
            ["AI:GeminiKey"] = apiKey,
            ["AI:GeminiFreeTierOnly"] = "true",
            ["AI:GeminiDailyRequestLimit"] = config["AI:GeminiDailyRequestLimit"] ?? "50",
            ["AI:GeminiRequestsPerMinute"] = config["AI:GeminiRequestsPerMinute"] ?? "10",
            ["Search:Provider"] = "hosted",
            ["Search:ExternalProcessingApproved"] = "true",
            ["Search:GeminiExternalProcessingApproved"] = "true",
            ["Search:ExternalImagesApproved"] = "true",
            ["Search:GeminiExternalImagesApproved"] = "true",
            ["Search:GeminiProviderDataCollection"] = "allow" // synthetic evaluation fixtures only
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private async Task<EvaluationCandidate[]> Rank(Guid runId, string modality, float[] query, string revision, CancellationToken cancellation)
    {
        var galleryCases = await db.AiEvaluationCases.Where(x => x.RunId == runId && x.Kind.StartsWith("embedding-passage") &&
                (x.State == "completed" || x.State == "cached")).ToArrayAsync(cancellation);
        var scores = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var row in galleryCases)
        {
            var input = JsonSerializer.Deserialize<EvaluationInput>(row.InputJson, JsonOptions)!;
            var vectorRow = await db.AiEvaluationVectors.SingleOrDefaultAsync(x => x.CacheKey == row.EmbeddingCacheKey && x.Revision == revision, cancellation);
            if (vectorRow is null) continue;
            var vector = JsonSerializer.Deserialize<float[]>(vectorRow.VectorJson, JsonOptions)!;
            var score = Cosine(query, vector);
            scores[input.ItemId!] = Math.Max(scores.GetValueOrDefault(input.ItemId!, -1d), score);
        }
        var names = GalleryNames();
        return scores.Select(pair => new EvaluationCandidate(pair.Key, names[pair.Key], Math.Round(pair.Value, 6)))
            .OrderByDescending(x => x.Similarity).ToArray();
    }

    private IConfiguration OpenRouterSettings()
    {
        var apiKey = config["AI:OpenRouterKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) throw new RuleException("ai_not_configured", 503);
        var values = new Dictionary<string, string?> {
            ["AI:Provider"] = "openrouter", ["AI:AllowNonSensitiveInference"] = "true", ["AI:AllowNonSensitiveImages"] = "true",
            ["AI:ProviderDataCollection"] = "allow",
            ["AI:OpenRouterKey"] = apiKey, ["AI:OpenRouterDailyRequestLimit"] = config["AI:OpenRouterDailyRequestLimit"],
            ["AI:OpenRouterRequestsPerMinute"] = config["AI:OpenRouterRequestsPerMinute"],
            ["AI:FaultTypes:0"] = FaultTypes[0], ["AI:FaultTypes:1"] = FaultTypes[1], ["AI:FaultTypes:2"] = FaultTypes[2],
            ["AI:Priorities:0"] = Priorities[0], ["AI:Priorities:1"] = Priorities[1], ["AI:Priorities:2"] = Priorities[2],
            ["AI:Risks:0"] = Priorities[0], ["AI:Risks:1"] = Priorities[1], ["AI:Risks:2"] = Priorities[2],
            ["Search:Provider"] = "hosted", ["Search:ExternalProcessingApproved"] = "true",
            ["Search:ExternalImagesApproved"] = "true", ["Search:ProviderDataCollection"] = "allow" // fixed synthetic maintenance inputs only
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private async Task<EvaluationUsage> CurrentUsage(CancellationToken cancellation, string provider = "openrouter")
    {
        var now = clock.GetUtcNow();
        var day = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var minute = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
        var gemini = provider == "gemini";
        var dailyLimit = config.GetValue(gemini ? "AI:GeminiDailyRequestLimit" : "AI:OpenRouterDailyRequestLimit", 50);
        var minuteLimit = config.GetValue(gemini ? "AI:GeminiRequestsPerMinute" : "AI:OpenRouterRequestsPerMinute", gemini ? 10 : 20);
        var entries = await db.InferenceUsage.Where(x => x.Provider == provider && (x.WindowStart == day || x.WindowStart == minute))
            .ToArrayAsync(cancellation);
        var dayUsed = entries.SingleOrDefault(x => x.Window == "day")?.Count ?? 0;
        var minuteUsed = entries.SingleOrDefault(x => x.Window == "minute")?.Count ?? 0;
        return new EvaluationUsage(dayUsed, dailyLimit, Math.Max(0, dailyLimit - dayUsed), minuteUsed, minuteLimit,
            Math.Max(0, minuteLimit - minuteUsed), day.AddDays(1), minute.AddMinutes(1),
            RemoteEmbeddings.Model, "nvidia/nemotron-3.5-super-vl:free", 0, false);
    }

    private async Task<int> CallsRequired(EvaluationSpec[] specs, CancellationToken cancellation)
    {
        var keys = specs.Where(x => x.CacheKey is not null).Select(x => x.CacheKey!).Distinct().ToArray();
        var existing = await db.AiEvaluationVectors.Where(x => keys.Contains(x.CacheKey) &&
                x.Revision == RemoteEmbeddings.Revision(config)).Select(x => x.CacheKey).ToArrayAsync(cancellation);
        return specs.Count(x => x.CacheKey is null || !existing.Contains(x.CacheKey, StringComparer.Ordinal));
    }

    private static EvaluationCaseResponse ToResponse(AiEvaluationCase row) => new(row.Id, row.RunId, row.CaseKey, row.Kind, row.State,
        JsonDocument.Parse(row.InputJson).RootElement.Clone(), JsonDocument.Parse(row.ExpectedJson).RootElement.Clone(),
        row.ActualJson is null ? null : JsonDocument.Parse(row.ActualJson).RootElement.Clone(),
        row.ErrorCode, row.Model, row.ElapsedMilliseconds, row.StartedAt, row.FinishedAt);

    private static EvaluationRunSummary ToSummary(AiEvaluationRun run) =>
        new(run.Id, run.Suite, run.SuiteVersion, run.State, run.CreatedAt, run.UpdatedAt);

    private static byte[] ImageBytes(string dataUrl) =>
        Convert.FromBase64String(dataUrl[(dataUrl.IndexOf(',') + 1)..]);

    private static string Csv(string value)
    {
        var safe = value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('-') || value.StartsWith('@') ? "'" + value : value;
        return "\"" + safe.Replace("\"", "\"\"") + "\"";
    }

    private static (int Count, int Agreed) ReferenceAgreement(IEnumerable<AiEvaluationCase> rows, string field)
    {
        var count = 0;
        var agreed = 0;
        foreach (var row in rows.Where(x => x.Kind.StartsWith("maintenance", StringComparison.Ordinal) && x.ActualJson is not null))
        {
            var expected = JsonText(row.ExpectedJson, field);
            var actual = JsonText(row.ActualJson!, field);
            if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual)) continue;
            count++;
            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)) agreed++;
        }
        return (count, agreed);
    }

    private static string? JsonText(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
    }

    private static bool? JsonBool(string json, string property)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : null;
    }

    private static double Cosine(float[] left, float[] right)
    {
        if (left.Length != right.Length) throw new RuleException("invalid_embedding", 503);
        var dot = 0d;
        for (var i = 0; i < left.Length; i++) dot += (double)left[i] * right[i];
        return dot;
    }

    private static EvaluationHistory[] History() =>
    [
        new("2026-10-30", "Maintenance text trial — Nemotron", "Category agreement 40/40; priority agreement 35/40; urgent cases 10/10. Labels were provisional and author-authored.", "Historical, different sample and references. No individual inputs/timings were imported.",
            ["Category agreement (%)", "Priority agreement (%)", "Urgent cases (%)"], [100, 87.5, 100]),
        new("2026-10-30", "Arabic ITSM labels — source-label agreement", "74/96 Arabic records agreed with source labels; source labels were not independently verified.", "Historical label agreement is not accuracy or an urgent-case evaluation.", ["Agreement (%)"], [74d / 96 * 100]),
        new("2026-10-30", "SigLIP 2 retrieval — separate 200-query bicycle set", "Top-1 166/200 (83%); Top-5 179/200 (89.5%). Different model and gallery.", "Historical retrieval result cannot be attributed to current Nemotron embeddings.", ["Top-1 (%)", "Top-5 (%)"], [83, 89.5]),
        new("2026-10-30", "SigLIP 2 Arabic gallery — 19 queries", "Top-1 16/19; Top-5 19/19. References were provisional.", "Small historical sample; not a current model result.", ["Top-1 (%)", "Top-5 (%)"], [16d / 19 * 100, 100]),
        new("2026-10-30", "No-match threshold stress — historical", "False accepts: threshold .85 = 70/200; .90 = 30/200; .95 = 2/200. No threshold was approved.", "Old SigLIP 2 experiment. Not an approved threshold or current lost-and-found model measurement.", ["Threshold .85 false accepts (%)", ".90 false accepts (%)", ".95 false accepts (%)"], [35, 15, 1]),
        new("2026-11-13", "Live OpenRouter smoke", "4/4 integration checks passed: text, image, text embedding, image embedding. This is connectivity/validation, not accuracy.", "Original route did not persist individual inputs or timings. New page runs store each step and duration.",
            ["Text", "Image", "Text embedding", "Image embedding"], [1, 1, 1, 1])
    ];

    private static EvaluationSpec[] Specifications(string suite)
    {
        var images = SyntheticImages();
        return suite switch
        {
            "integration" =>
            [
                ClassCase("integration-text", "maintenance-text", "TEST ONLY. Fictional pipe leak in an empty imaginary room.", null, new("plumbing", "medium", "medium", "provisional example reference")),
                ClassCase("integration-image", "maintenance-image", "TEST ONLY. Classify the fictional maintenance defect visible in this image.", images["leak"], new("plumbing", "medium", "medium", "provisional example reference")),
                EmbedCase("integration-embedding-text", "embedding-query-text", "TEST ONLY. Fictional blue marker beside an imaginary leaking pipe.", null, "integration:text:v1", null, "text"),
                EmbedCase("integration-embedding-image", "embedding-image", null, images["leak"], "integration:image:v1", null, "image")
            ],
            "maintenance-text" => TextExamples().Select(x => ClassCase(x.Id, "maintenance-text", x.Text, null,
                new(x.Type, x.Priority, x.Risk, "assistant-authored provisional reference"))).ToArray(),
            "maintenance-image" => new[] { "leak", "spark", "light", "wall" }.Select(name =>
                ClassCase("image-" + name, "maintenance-image", "TEST ONLY. Classify only the visible fictional maintenance defect.", images[name],
                    new(name == "spark" ? "electrical" : name == "light" ? "electrical" : name == "wall" ? "other" : "plumbing",
                        name == "spark" ? "high" : name == "wall" ? "low" : "medium",
                        name == "spark" ? "high" : "low", "illustrative synthetic visual reference; not expert validated"))).ToArray(),
            "lost-found" => LostFoundSpecs(images),
            _ => throw new RuleException("ai_evaluation_suite_invalid", 400)
        };
    }

    private static EvaluationSpec ClassCase(string key, string kind, string text, string? image, EvaluationExpected expected) =>
        new(key, kind, new(text, image is null ? null : Convert.ToBase64String(Convert.FromBase64String(image))), expected, null);

    private static EvaluationSpec EmbedCase(string key, string kind, string? text, string? image, string cache, string? itemId, string modality) =>
        new(key, kind, new(text, image, itemId, modality), new(null, null, null, null, itemId), cache);

    private static EvaluationSpec[] LostFoundSpecs(Dictionary<string, string> images)
    {
        var gallery = Gallery();
        var cases = gallery.SelectMany(pair => new[] {
            EmbedCase("gallery-" + pair.Key + "-text", "embedding-passage-text", pair.Value.Description, null, "gallery:" + pair.Key + ":text:v1", pair.Key, "text"),
            EmbedCase("gallery-" + pair.Key + "-image", "embedding-passage-image", null, images[pair.Value.Image], "gallery:" + pair.Key + ":image:v1", pair.Key, "image")
        }).ToList();
        var queries = new (string Id, string Kind, string? Text, string? Image, string? Match, string Modality)[] {
            ("query-01-text", "embedding-query-text", "A silver water bottle with a loop cap", null, "bottle", "text"),
            ("query-02-text", "embedding-query-text", "Black wireless headphones in a hard case", null, "headphones", "text"),
            ("query-03-text", "embedding-query-text", "مظلة حمراء قابلة للطي", null, "umbrella", "text"),
            ("query-04-text", "embedding-query-text", "محفظة جلدية بنية وبها بطاقة خيالية", null, "wallet", "text"),
            ("query-05-image", "embedding-query-image", null, images["bottle"], "bottle", "image"),
            ("query-06-image", "embedding-query-image", null, images["watch"], "watch", "image"),
            ("query-07-absent-text", "embedding-query-text", "A fictional purple violin case with brass corners", null, null, "text"),
            ("query-08-absent-image", "embedding-query-image", null, images["query-absent"], null, "image")
        };
        cases.AddRange(queries.Select(q => EmbedCase(q.Id, q.Kind, q.Text, q.Image,
            "query:" + q.Id + (q.Id == "query-08-absent-image" ? ":v2" : ":v1"), q.Match, q.Modality)));
        return cases.ToArray();
    }

    private static Dictionary<string, GalleryFixture> Gallery() => new(StringComparer.Ordinal) {
        ["bottle"] = new("Stainless steel bottle with loop cap", "bottle"),
        ["headphones"] = new("Black over-ear wireless headphones", "headphones"),
        ["umbrella"] = new("Red folding umbrella with curved handle", "umbrella"),
        ["wallet"] = new("Brown leather wallet with fictional card", "wallet"),
        ["watch"] = new("Silver analog wristwatch with black strap", "watch"),
        ["notebook"] = new("Blue spiral notebook with blank cover", "notebook")
    };

    private static Dictionary<string, string> GalleryNames() =>
        Gallery().ToDictionary(x => x.Key, x => x.Value.Description, StringComparer.Ordinal);

    private static MaintenanceFixture[] TextExamples() =>
    [
        new("text-ar-01", "يوجد تسرب مياه أسفل الحوض والأرضية بدأت تبتل.", "plumbing", "medium", "medium"),
        new("text-ar-02", "رائحة احتراق من لوحة الكهرباء والغطاء ساخن.", "electrical", "high", "high"),
        new("text-ar-03", "المصباح في القاعة لا يعمل منذ الصباح.", "electrical", "low", "low"),
        new("text-ar-04", "شرخ في الجدار يحتاج إلى معاينة.", "other", "low", "low"),
        new("text-eg-01", "الحنفية بتسرب جامد والميه مغرقة البلاط جنبها.", "plumbing", "medium", "medium"),
        new("text-eg-02", "في شرارة طالعة من الفيشة وفي ريحة شياط.", "electrical", "high", "high"),
        new("text-eg-03", "لمبة الفصل اتحرقت ومش منورة.", "electrical", "low", "low"),
        new("text-eg-04", "باب القاعة مش بيقفل كويس والمفصلة سايبة.", "other", "medium", "low"),
        new("text-en-01", "A pipe is leaking near the sink and water is spreading across the floor.", "plumbing", "medium", "medium"),
        new("text-en-02", "An outlet is sparking and smells burned; keep people away.", "electrical", "high", "high"),
        new("text-en-03", "One ceiling lamp is out in an otherwise lit room.", "electrical", "low", "low"),
        new("text-en-04", "A cabinet door hinge is loose and the door will not close.", "other", "medium", "low")
    ];

    private static Dictionary<string, string> SyntheticImages()
    {
        string[] names = ["leak", "spark", "light", "wall", "query-absent", "bottle", "headphones", "umbrella", "wallet", "watch", "notebook"];
        return names.ToDictionary(x => x, x => Convert.ToBase64String(DrawFixture(x)), StringComparer.Ordinal);
    }

    private static byte[] DrawFixture(string name)
    {
        using var bitmap = new SKBitmap(320, 220);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(245, 248, 252));
        using var outline = new SKPaint { Color = new SKColor(48, 62, 82), StrokeWidth = 9, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
        using var blue = new SKPaint { Color = new SKColor(65, 132, 207), IsAntialias = true };
        using var orange = new SKPaint { Color = new SKColor(236, 144, 55), IsAntialias = true };
        if (name == "leak") { canvas.DrawLine(160, 25, 160, 92, outline); canvas.DrawOval(new SKRect(127, 85, 193, 128), blue); canvas.DrawLine(110, 160, 210, 160, outline); canvas.DrawLine(120, 160, 102, 193, outline); canvas.DrawLine(200, 160, 218, 193, outline); }
        else if (name == "spark") { canvas.DrawRoundRect(new SKRect(100, 58, 220, 170), 12, 12, outline); canvas.DrawCircle(135, 110, 9, blue); canvas.DrawCircle(185, 110, 9, blue); canvas.DrawLine(158, 35, 145, 65, orange); canvas.DrawLine(175, 38, 168, 69, orange); }
        else if (name == "light") { canvas.DrawCircle(160, 93, 48, orange); canvas.DrawRoundRect(new SKRect(138, 135, 182, 170), 6, 6, outline); canvas.DrawLine(145, 179, 175, 179, outline); }
        else if (name == "wall") { canvas.DrawRect(new SKRect(54, 34, 266, 190), outline); canvas.DrawLine(156, 50, 138, 88, outline); canvas.DrawLine(138, 88, 176, 117, outline); canvas.DrawLine(176, 117, 150, 155, outline); }
        else if (name == "query-absent")
        {
            using var purple = new SKPaint { Color = new SKColor(103, 69, 150), IsAntialias = true };
            using var brass = new SKPaint { Color = new SKColor(193, 147, 64), StrokeWidth = 7, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawOval(new SKRect(65, 31, 255, 190), purple);
            canvas.DrawOval(new SKRect(82, 45, 238, 176), brass);
            canvas.DrawLine(160, 48, 160, 171, brass);
            canvas.DrawCircle(160, 110, 9, brass);
        }
        else if (name.Contains("bottle", StringComparison.Ordinal)) { canvas.DrawRoundRect(new SKRect(118, 55, 202, 180), 28, 28, blue); canvas.DrawRoundRect(new SKRect(140, 35, 180, 60), 7, 7, outline); canvas.DrawCircle(160, 35, 17, outline); }
        else if (name == "headphones") { canvas.DrawArc(new SKRect(92, 35, 228, 170), 180, 180, false, outline); canvas.DrawRoundRect(new SKRect(84, 105, 120, 177), 14, 14, blue); canvas.DrawRoundRect(new SKRect(200, 105, 236, 177), 14, 14, blue); }
        else if (name == "umbrella") { canvas.DrawArc(new SKRect(60, 40, 260, 165), 180, 180, true, blue); canvas.DrawLine(160, 102, 160, 185, outline); canvas.DrawLine(160, 185, 187, 185, outline); }
        else if (name == "wallet") { canvas.DrawRoundRect(new SKRect(82, 70, 238, 160), 18, 18, new SKPaint { Color = new SKColor(132, 90, 59), IsAntialias = true }); canvas.DrawLine(165, 70, 165, 160, outline); }
        else if (name == "watch") { canvas.DrawRoundRect(new SKRect(135, 22, 185, 198), 12, 12, outline); canvas.DrawCircle(160, 110, 47, blue); canvas.DrawLine(160, 110, 160, 78, outline); canvas.DrawLine(160, 110, 183, 123, outline); }
        else { canvas.DrawRoundRect(new SKRect(88, 44, 232, 180), 9, 9, blue); canvas.DrawLine(112, 75, 208, 75, outline); canvas.DrawLine(112, 99, 195, 99, outline); }
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 90);
        return png.ToArray();
    }

    private sealed record EvaluationSpec(string CaseKey, string Kind, EvaluationInput Input, EvaluationExpected Expected, string? CacheKey);
    private sealed record EvaluationInput(string? Text, string? ImageDataUrl, string? ItemId = null, string? Modality = null);
    private sealed record EvaluationExpected(string? Type, string? Priority, string? Risk, string? Note, string? ItemId = null);
    private sealed record GalleryFixture(string Description, string Image);
    private sealed record MaintenanceFixture(string Id, string Text, string Type, string Priority, string Risk);
    private sealed record StepOutput(string Json, string Model, long ElapsedMilliseconds, bool Cached)
    {
        public static StepOutput Create(object actual, string model, long elapsed, bool cached = false) =>
            new(JsonSerializer.Serialize(actual, JsonOptions), model, elapsed, cached);
    }

    private sealed record EvaluationCandidate(string ItemId, string Name, double Similarity);
    public sealed record CreateEvaluationRun(string Suite);
    public sealed record EvaluationPreflight(string Suite, string SuiteVersion, int CaseCount, int ApiCallsRequired,
        EvaluationUsage Usage, string Status);
    public sealed record EvaluationUsage(int DailyUsed, int DailyLimit, int DailyRemaining, int MinuteUsed, int MinuteLimit,
        int MinuteRemaining, DateTimeOffset NextDayAt, DateTimeOffset NextMinuteAt, string EmbeddingModel, string ClassificationModel,
        decimal MaximumPrice, bool PaidFallbacks);
    public sealed record EvaluationHistory(string Date, string Source, string Metrics, string Limits, string[] ChartLabels, double[] ChartValues);
    public sealed record EvaluationRunSummary(Guid Id, string Suite, string SuiteVersion, string State, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    public sealed record EvaluationCaseResponse(Guid Id, Guid RunId, string CaseKey, string Kind, string State, JsonElement Input, JsonElement Expected,
        JsonElement? Actual, string? ErrorCode, string? Model, long? ElapsedMilliseconds, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);
    public sealed record EvaluationSnapshot(EvaluationUsage Usage, EvaluationRunSummary[] Runs, EvaluationCaseResponse[] Cases, EvaluationHistory[] History);
    public sealed record EvaluationRunCreated(Guid Id, string Suite, int CaseCount, int ApiCallsRequired, EvaluationUsage Usage, string SuiteVersion);
    public sealed record EvaluationStepResponse(Guid RunId, string RunState, EvaluationCaseResponse? Case, EvaluationUsage Usage, string? PauseReason = null);
    public sealed record EvaluationRunDetail(EvaluationRunSummary Run, EvaluationCaseResponse[] Cases);
}
