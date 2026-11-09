using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed class JobWorker(IServiceScopeFactory scopes, ILogger<JobWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunNext(stoppingToken); }
            catch (Exception exception) when (exception is not OperationCanceledException)
            { logger.LogWarning("Job worker unavailable: {ExceptionType}", exception.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task RunNext(CancellationToken cancellation)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var job = await db.Jobs.FromSqlRaw("SELECT * FROM smart_campus.\"Jobs\" WHERE \"State\" IN ('pending','running') AND \"AvailableAt\" <= now() ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED")
            .FirstOrDefaultAsync(cancellation);
        if (job is null) return;
        job.State = "running";
        job.Attempts++;
        job.AvailableAt = DateTimeOffset.UtcNow.AddMinutes(2);
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        try
        {
            if (job.Kind == "email") await scope.ServiceProvider.GetRequiredService<EmailSender>().Deliver(job.EntityId);
            else if (job.Kind == "index") await scope.ServiceProvider.GetRequiredService<RetrievalService>().Index(job.EntityId);
            else await Assess(scope.ServiceProvider, db, job, cancellation);
            job.State = "completed";
        }
        catch (Exception exception) when (exception is RuleException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or MailKit.ServiceNotConnectedException or MailKit.Security.AuthenticationException or MailKit.Net.Smtp.SmtpCommandException or System.IO.IOException)
        {
            job.State = job.Attempts >= 3 || exception is RuleException { Code: "ai_not_configured" or "taxonomy_unavailable" or "email_not_configured" or "email_rejected" } ? "unavailable" : "pending";
            job.AvailableAt = DateTimeOffset.UtcNow.AddMinutes(job.Attempts * 2);
            var assessment = await db.Assessments.SingleOrDefaultAsync(a => a.ReportId == job.EntityId && a.InputVersion == job.InputVersion, cancellation);
            if (assessment is not null) { assessment.State = "unavailable"; assessment.FailureCode = exception is RuleException rule ? rule.Code : "provider_unavailable"; }
            logger.LogWarning("Job {JobId} returned {State}", job.Id, job.State);
        }
        await db.SaveChangesAsync(cancellation);
    }

    private static async Task Assess(IServiceProvider services, CampusDb db, BackgroundJob job, CancellationToken cancellation)
    {
        var report = await db.Reports.FindAsync([job.EntityId], cancellation);
        if (report is null || report.CancelledAt.HasValue || report.ContentVersion != job.InputVersion) return;
        byte[]? image = null;
        if (services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>().GetValue<bool>("AI:AllowNonSensitiveImages"))
        {
            var attachment = await db.Media.FirstOrDefaultAsync(m => m.ReportId == report.Id, cancellation);
            if (attachment is not null) image = await services.GetRequiredService<PrivateMedia>().Read(attachment);
        }
        var suggestion = await services.GetRequiredService<AiProviders>().Assess(report.Description, cancellation, image);
        await db.Entry(report).ReloadAsync(cancellation);
        if (report.ContentVersion != job.InputVersion || report.CancelledAt.HasValue) return;
        var assessment = await db.Assessments.SingleAsync(a => a.ReportId == report.Id && a.InputVersion == job.InputVersion, cancellation);
        assessment.State = "completed";
        assessment.FaultType = suggestion.Type;
        assessment.Priority = suggestion.Priority;
        assessment.Risk = suggestion.Risk;
        assessment.Explanation = suggestion.Explanation;
        assessment.Model = services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["AI:Provider"] == "nvidia" ? "openai/gpt-oss-20b" : "nvidia/nemotron-3.5-super-vl:free";
    }
}
