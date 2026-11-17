using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>Mutates the tracked assessment; the durable worker commits it with the job result.</summary>
public sealed class MaintenanceAssessmentProcessor(CampusDb db, PrivateMedia media, AiProviders providers, IConfiguration config)
{
    public async Task Process(BackgroundJob job, CancellationToken cancellation)
    {
        var report = await db.Reports.FindAsync([job.EntityId], cancellation);
        if (report is null || report.CancelledAt.HasValue || report.ContentVersion != job.InputVersion) return;
        var assessment = await db.Assessments.SingleAsync(a => a.ReportId == report.Id && a.InputVersion == job.InputVersion, cancellation);
        if (assessment.ReviewedBy is null || assessment.ReviewedAt is null) throw new RuleException("ai_input_review_required", 503);
        byte[]? image = null;
        if (assessment.ReviewedImageId.HasValue)
        {
            if (!config.GetValue<bool>("AI:AllowNonSensitiveImages")) throw new RuleException("ai_input_review_required", 503);
            var attachment = await db.Media.SingleOrDefaultAsync(m => m.Id == assessment.ReviewedImageId && m.ReportId == report.Id, cancellation)
                ?? throw new RuleException("ai_input_review_required", 503);
            image = await media.Read(attachment);
        }
        AiSuggestion suggestion;
        try { suggestion = await providers.Assess(report.Description, cancellation, image); }
        catch (RuleException failure) when (UseKeywordDraft(failure, job))
        {
            // No AI provider could answer: keep the queue moving with a clearly labelled keyword draft.
            suggestion = RuleTriage.Suggest(report.Description, config.GetSection("AI:FaultTypes").Get<string[]>() ?? ["other"],
                config.GetSection("AI:Priorities").Get<string[]>() ?? ["low"], config.GetSection("AI:Risks").Get<string[]>() ?? ["low"]);
        }
        await db.Entry(report).ReloadAsync(cancellation);
        if (report.ContentVersion != job.InputVersion || report.CancelledAt.HasValue)
        {
            assessment.State = "unavailable";
            assessment.FailureCode = "input_changed";
            return;
        }
        assessment.State = "completed";
        assessment.FailureCode = null;
        assessment.FaultType = suggestion.Type;
        assessment.Priority = suggestion.Priority;
        assessment.Risk = suggestion.Risk;
        assessment.Explanation = AssessmentExplanationText.Encode(suggestion.ExplanationAr, suggestion.ExplanationEn);
        assessment.Model = suggestion.Model ?? AiProviders.SelectedModel(config, image is not null);
    }

    // Quota, key or outage failures, or the last retry: more waiting would not help staff.
    private static bool UseKeywordDraft(RuleException failure, BackgroundJob job) =>
        (failure.Code is "ai_quota_exhausted" or "ai_key_rejected" or "ai_access_denied") ||
        (job.Attempts >= 3 && failure.Code is "ai_rate_limited" or "ai_unavailable" or "ai_connection_failed" or "ai_timeout"
            or "invalid_ai_response" or "ai_request_rejected");
}
