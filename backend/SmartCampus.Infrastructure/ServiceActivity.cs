using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>Stores fixed service outcomes only, independently of the caller's unit of work.</summary>
public sealed class ServiceActivity(IServiceScopeFactory scopes, ILogger<ServiceActivity> logger)
{
    public async Task Record(string service, bool succeeded)
    {
        if (service is not ("gemini" or "maintenance" or "email"))
            throw new ArgumentOutOfRangeException(nameof(service));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Audit.Add(new AuditEvent {
                Action = $"Service:{service}:{(succeeded ? "success" : "failure")}",
                EntityId = Guid.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync(timeout.Token);
        }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException or OperationCanceledException or TimeoutException)
        {
            // A missing diagnostic record must not turn a successful provider call into a retry.
            logger.LogWarning("Service activity could not be persisted for {Service}", service);
        }
    }
}
