using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class StatisticsMetricRegressionTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Workload_metrics_count_only_non_cancelled_reports_and_average_valid_resolution_times()
    {
        using var admin = await campus.Login(campus.Admin);
        var now = DateTimeOffset.UtcNow;

        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Reports.AddRange(
                Report("resolved 72h", "resolved", now.AddDays(-6), now.AddDays(-3), faultType: "TEST confirmed leak"),
                Report("resolved 48h", "resolved", now.AddDays(-4), now.AddDays(-2), faultType: "TEST confirmed leak"),
                Report("oldest open", "review", now.AddDays(-5), faultType: "TEST confirmed leak"),
                Report("cancelled before handling", "submitted", now.AddDays(-1), cancelledAt: now.AddHours(-12)),
                Report("rejected", "rejected", now.AddDays(-1)));
            await db.SaveChangesAsync();
        }

        var response = await CampusFixture.Response(await admin.GetAsync("/api/v1/statistics"));
        Assert.Equal(5, response.GetProperty("totalReports").GetInt32());
        Assert.Equal(2, response.GetProperty("byStatus").GetProperty("resolved").GetInt32());
        Assert.Equal(1, response.GetProperty("byStatus").GetProperty("cancelled").GetInt32());
        Assert.Equal(1, response.GetProperty("openReports").GetInt32());
        Assert.Equal(0.5, response.GetProperty("resolutionRate").GetDouble());
        Assert.Equal(60, response.GetProperty("averageResolutionHours").GetDouble());
        Assert.InRange(response.GetProperty("oldestOpenDays").GetDouble(), 4.99, 5.01);
        Assert.Equal(3, Assert.Single(response.GetProperty("recurrence").EnumerateArray())
            .GetProperty("count").GetInt32());
        Assert.Equal(8, response.GetProperty("weeklyTrend").GetArrayLength());
    }

    private SmartCampus.Domain.MaintenanceReport Report(string description, string status,
        DateTimeOffset createdAt, DateTimeOffset? resolvedAt = null, DateTimeOffset? cancelledAt = null,
        string? faultType = null) => new()
    {
        ReporterId = campus.Student,
        DepartmentId = campus.Department,
        FacultyId = campus.Faculty,
        BuildingId = campus.Building,
        FloorId = campus.Floor,
        Description = "TEST " + description,
        Details = "TEST synthetic statistics record",
        Status = status,
        CreatedAt = createdAt,
        ResolvedAt = resolvedAt,
        CancelledAt = cancelledAt,
        FaultType = faultType
    };
}
