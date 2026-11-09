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
        var suggestion = await providers.Assess(report.Description, cancellation, image);
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
        assessment.Explanation = suggestion.Explanation;
        assessment.Model = config["AI:Provider"] == "nvidia" ? "openai/gpt-oss-20b" : "nvidia/nemotron-3.5-super-vl:free";
    }
}
