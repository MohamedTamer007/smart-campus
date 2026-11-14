using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class JobWorkerLeaseTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Expired_leases_stop_at_three_attempts_and_preserve_completed_results()
    {
        using var student = await campus.Login(campus.Student);
        var reportId = await campus.Report(student);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var exhausted = await db.Jobs.SingleAsync(j => j.EntityId == reportId && j.Kind == "maintenance");
        exhausted.State = "running";
        exhausted.Attempts = 3;
        exhausted.AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        var future = new BackgroundJob { Kind = "maintenance", EntityId = Guid.NewGuid(), Key = "live-lease", State = "running",
            Attempts = 1, AvailableAt = DateTimeOffset.UtcNow.AddHours(1) };
        var finalAttempt = new BackgroundJob { Kind = "maintenance", EntityId = Guid.NewGuid(), Key = "last-eligible-attempt",
            State = "running", Attempts = 2, AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1), CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1) };
        db.Jobs.AddRange(future, finalAttempt);
        await db.SaveChangesAsync();
        var worker = new JobWorker(campus.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);

        await worker.RunNext(CancellationToken.None);
        await db.Entry(exhausted).ReloadAsync();
        Assert.Equal("unavailable", exhausted.State);
        Assert.Equal(3, exhausted.Attempts);
        var assessment = await db.Assessments.AsNoTracking().SingleAsync(a => a.ReportId == reportId);
        Assert.Equal("unavailable", assessment.State);
        Assert.Equal("job_attempts_exhausted", assessment.FailureCode);

        await worker.RunNext(CancellationToken.None);
        await db.Entry(finalAttempt).ReloadAsync();
        Assert.Equal("completed", finalAttempt.State); // nonexistent report is an idempotent no-op, with no provider request
        Assert.Equal(3, finalAttempt.Attempts);
        await worker.RunNext(CancellationToken.None);
        await db.Entry(future).ReloadAsync();
        await db.Entry(finalAttempt).ReloadAsync();
        Assert.Equal("running", future.State);
        Assert.Equal(1, future.Attempts);
        Assert.Equal("completed", finalAttempt.State);
        Assert.Equal(3, finalAttempt.Attempts);
    }

    [DatabaseFact]
    public async Task Exhausted_lease_does_not_erase_an_already_persisted_ai_result()
    {
        using var student = await campus.Login(campus.Student);
        var reportId = await campus.Report(student);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var assessment = await db.Assessments.SingleAsync(a => a.ReportId == reportId);
        assessment.State = "completed";
        assessment.FaultType = "plumbing";
        assessment.Priority = "high";
        assessment.Risk = "medium";
        assessment.Explanation = "Synthetic saved result";
        assessment.Model = "synthetic-model";
        var job = await db.Jobs.SingleAsync(j => j.EntityId == reportId && j.Kind == "maintenance");
        job.State = "running";
        job.Attempts = 3;
        job.AvailableAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
        var worker = new JobWorker(campus.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<JobWorker>.Instance);
        await worker.RunNext(CancellationToken.None);
        var saved = await db.Assessments.AsNoTracking().SingleAsync(a => a.ReportId == reportId);
        await db.Entry(job).ReloadAsync();
        Assert.Equal("completed", job.State);
        Assert.Equal(3, job.Attempts);
        Assert.Equal("completed", saved.State);
        Assert.Null(saved.FailureCode);
        Assert.Equal("plumbing", saved.FaultType);
        Assert.Equal("high", saved.Priority);
        Assert.Equal("medium", saved.Risk);
        Assert.Equal("Synthetic saved result", saved.Explanation);
        Assert.Equal("synthetic-model", saved.Model);
    }
}
