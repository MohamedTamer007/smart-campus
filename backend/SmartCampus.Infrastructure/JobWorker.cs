using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Sockets;
using System.Security.Authentication;
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
            {
                // Never log connection strings, server messages, exception.ToString(), or secrets.
                // Emit safe error categories so a pooler auth failure can be distinguished
                // from TLS validation, DNS, network timeouts, or a database outage.
                logger.LogWarning("Job worker unavailable: {ExceptionType}, {FailureCategory}",
                    exception.GetType().Name, DatabaseFailureCategory(exception));
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private static string DatabaseFailureCategory(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg)
                return pg.SqlState switch
                {
                    PostgresErrorCodes.InvalidPassword => "invalid_db_password",
                    PostgresErrorCodes.InvalidAuthorizationSpecification => "db_auth_or_pooler_user",
                    PostgresErrorCodes.CannotConnectNow => "database_unavailable",
                    _ => "postgres_sqlstate_" + pg.SqlState
                };
            if (current is AuthenticationException) return "tls_certificate_or_handshake";
            if (current is SocketException socket)
                return socket.SocketErrorCode switch
                {
                    SocketError.HostNotFound => "dns_host_not_found",
                    SocketError.TryAgain => "dns_retry",
                    SocketError.ConnectionRefused => "network_connection_refused",
                    SocketError.TimedOut => "network_timeout",
                    SocketError.NetworkUnreachable => "network_unreachable",
                    _ => "socket_" + socket.SocketErrorCode
                };
            if (current is TimeoutException) return "connection_timeout";
        }
        return "database_or_network_other";
    }

    public async Task RunNext(CancellationToken cancellation)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var job = await db.Jobs.FromSqlRaw("SELECT * FROM smart_campus.\"Jobs\" WHERE \"State\" IN ('pending','running') AND \"AvailableAt\" <= now() ORDER BY \"CreatedAt\" LIMIT 1 FOR UPDATE SKIP LOCKED")
            // The raw SQL already applies eligibility, deterministic ordering, and LIMIT 1.
            // SingleOrDefault avoids EF's FirstWithoutOrderByAndFilter warning, which cannot
            // see the ORDER BY and WHERE clauses embedded in FromSqlRaw.
            .SingleOrDefaultAsync(cancellation);
        if (job is null) return;
        // A process may stop before the provider result is persisted. Reclaiming
        // an expired lease must still respect the same bounded attempt budget.
        if (job.Attempts >= 3)
        {
            job.State = "unavailable";
            if (job.Kind == "maintenance")
            {
                var exhausted = await db.Assessments.SingleOrDefaultAsync(a => a.ReportId == job.EntityId && a.InputVersion == job.InputVersion, cancellation);
                if (exhausted?.State == "completed") job.State = "completed";
                else if (exhausted is not null)
                {
                    exhausted.State = "unavailable";
                    exhausted.FailureCode = "job_attempts_exhausted";
                }
            }
            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
            return;
        }
        job.State = "running";
        job.Attempts++;
        job.AvailableAt = DateTimeOffset.UtcNow.AddMinutes(2);
        await db.SaveChangesAsync(cancellation);
        await transaction.CommitAsync(cancellation);
        try
        {
            if (job.Kind == "email") await scope.ServiceProvider.GetRequiredService<EmailSender>().Deliver(job.EntityId);
            else if (job.Kind == "index") await scope.ServiceProvider.GetRequiredService<RetrievalService>().Index(job.EntityId);
            else await scope.ServiceProvider.GetRequiredService<MaintenanceAssessmentProcessor>().Process(job, cancellation);
            job.State = "completed";
        }
        catch (Exception exception) when (exception is RuleException or HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or MailKit.ServiceNotConnectedException or MailKit.Security.AuthenticationException or MailKit.Net.Smtp.SmtpCommandException or System.IO.IOException)
        {
            if (exception is RuleException { Code: "ai_quota_exhausted" }) job.Attempts--;
            job.State = job.Attempts >= 3 || exception is RuleException { Code: "ai_not_configured" or "search_provider_not_configured" or "search_external_images_not_approved" or "taxonomy_unavailable" or "ai_quota_not_configured" or "ai_input_review_required" or "email_not_configured" or "email_test_recipient_required" or "email_recipient_not_allowed" or "email_rejected" or "release_restricted" } ? "unavailable" : "pending";
            job.AvailableAt = exception is RuleException { RetryAt: { } retryAt } ? retryAt : DateTimeOffset.UtcNow.AddMinutes(job.Attempts * 2);
            var assessment = await db.Assessments.SingleOrDefaultAsync(a => a.ReportId == job.EntityId && a.InputVersion == job.InputVersion, cancellation);
            if (assessment is not null)
            {
                // A retryable job is still queued; expose that state instead of
                // telling staff the provider is unavailable before retries end.
                assessment.State = job.State == "unavailable" ? "unavailable" : "pending";
                assessment.FailureCode = job.State == "unavailable"
                    ? exception is RuleException rule ? rule.Code : "provider_unavailable"
                    : null;
            }
            logger.LogWarning("Job {JobId} returned {State}", job.Id, job.State);
        }
        await db.SaveChangesAsync(cancellation);
    }

}
