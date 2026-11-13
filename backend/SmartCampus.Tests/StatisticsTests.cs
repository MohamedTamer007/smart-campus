using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class StatisticsTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Statistics_expose_only_staff_confirmed_priority_and_risk_decisions()
    {
        using var admin = await campus.Login(campus.Admin);
        var now = DateTimeOffset.UtcNow;
        var aiOnlyReport = new SmartCampus.Domain.MaintenanceReport
        {
            ReporterId = campus.Student,
            DepartmentId = campus.Department,
            FacultyId = campus.Faculty,
            BuildingId = campus.Building,
            FloorId = campus.Floor,
            Description = "TEST AI suggestion only",
            Details = "TEST synthetic",
            Status = "review",
            CreatedAt = now.AddMinutes(-2)
        };
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Reports.AddRange(
                new SmartCampus.Domain.MaintenanceReport
                {
                    ReporterId = campus.Student, DepartmentId = campus.Department,
                    FacultyId = campus.Faculty, BuildingId = campus.Building, FloorId = campus.Floor,
                    Description = "TEST staff high decision", Details = "TEST synthetic", Status = "repair",
                    CreatedAt = now.AddMinutes(-4), FaultType = "plumbing", Priority = "high", Risk = "high"
                },
                new SmartCampus.Domain.MaintenanceReport
                {
                    ReporterId = campus.Student, DepartmentId = campus.Department,
                    FacultyId = campus.Faculty, BuildingId = campus.Building, FloorId = campus.Floor,
                    Description = "TEST staff medium decision", Details = "TEST synthetic", Status = "resolved",
                    CreatedAt = now.AddMinutes(-3), FaultType = "electrical", Priority = "medium", Risk = "low"
                },
                aiOnlyReport);
            db.Assessments.Add(new SmartCampus.Domain.Assessment
            {
                ReportId = aiOnlyReport.Id,
                InputVersion = 1,
                State = "completed",
                FaultType = "plumbing",
                Priority = "high",
                Risk = "high"
            });
            await db.SaveChangesAsync();
        }

        var response = await CampusFixture.Response(await admin.GetAsync("/api/v1/statistics"));
        var priorities = response.GetProperty("priorities");
        var risks = response.GetProperty("risks");
        Assert.Equal(1, priorities.GetProperty("high").GetInt32());
        Assert.Equal(1, priorities.GetProperty("medium").GetInt32());
        Assert.Equal(2, priorities.EnumerateObject().Count());
        Assert.Equal(1, risks.GetProperty("high").GetInt32());
        Assert.Equal(1, risks.GetProperty("low").GetInt32());
        Assert.Equal(2, risks.EnumerateObject().Count());
        Assert.Equal(1, response.GetProperty("faultTypes").GetProperty("plumbing").GetInt32());
    }

    [DatabaseFact]
    public async Task Admin_statistics_return_named_buildings_workload_metrics_and_save_map_positions_with_version_checks()
    {
        using var admin = await campus.Login(campus.Admin);
        var now = DateTimeOffset.UtcNow;
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Reports.AddRange(
                new SmartCampus.Domain.MaintenanceReport { ReporterId = campus.Student, DepartmentId = campus.Department, FacultyId = campus.Faculty,
                    BuildingId = campus.Building, FloorId = campus.Floor, Description = "TEST report resolved", Details = "TEST details", Status = "resolved",
                    CreatedAt = now.AddHours(-10), ResolvedAt = now.AddHours(-2), FaultType = "TEST staff decision" },
                new SmartCampus.Domain.MaintenanceReport { ReporterId = campus.Student, DepartmentId = campus.Department, FacultyId = campus.Faculty,
                    BuildingId = campus.Building, FloorId = campus.Floor, Description = "TEST report open", Details = "TEST details", Status = "review",
                    CreatedAt = now.AddDays(-4), FaultType = "TEST staff decision" });
            await db.SaveChangesAsync();
        }

        var unauthorized = await campus.Login(campus.Staff);
        using (unauthorized)
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await unauthorized.PutAsJsonAsync(
                $"/api/v1/admin/locations/{campus.Building}/position", new { x = 25, y = 30, version = 1 })).StatusCode);
        }
        var moved = await admin.PutAsJsonAsync($"/api/v1/admin/locations/{campus.Building}/position", new { x = 25, y = 30, version = 1 });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(2, (await moved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync(
            $"/api/v1/admin/locations/{campus.Building}/position", new { x = 28, y = 32, version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(
            $"/api/v1/admin/locations/{campus.Building}/position", new { x = 101, y = 32, version = 2 })).StatusCode);

        var response = await CampusFixture.Response(await admin.GetAsync("/api/v1/statistics"));
        Assert.Equal(2, response.GetProperty("totalReports").GetInt32());
        Assert.Equal(1, response.GetProperty("openReports").GetInt32());
        Assert.Equal(0.5, response.GetProperty("resolutionRate").GetDouble());
        Assert.Equal(8, response.GetProperty("averageResolutionHours").GetDouble());
        Assert.Equal("TEST ONLY BUILDING", response.GetProperty("buildings")[0].GetProperty("en").GetString());
        Assert.Equal(25, response.GetProperty("buildings")[0].GetProperty("x").GetDouble());
        Assert.Equal(2, response.GetProperty("recurrence")[0].GetProperty("count").GetInt32());
        Assert.Equal(8, response.GetProperty("weeklyTrend").GetArrayLength());
    }

    [Theory]
    [InlineData("2026-04-23", "2026-04-22T22:00:00Z")]
    [InlineData("2026-04-24", "2026-04-23T22:00:00Z")]
    [InlineData("2026-04-25", "2026-04-24T21:00:00Z")]
    public void Cairo_calendar_includes_first_actual_instant_across_DST(string day, string utc) =>
        Assert.Equal(DateTimeOffset.Parse(utc), CairoCalendar.StartOfDay(DateOnly.Parse(day)));

    [DatabaseFact]
    public async Task New_claim_on_old_item_is_counted_by_claim_date_and_department()
    {
        using var finder = await campus.Login(campus.Student);
        using var requester = await campus.Login(campus.Other);
        using var staff = await campus.Login(campus.Staff);
        using var admin = await campus.Login(campus.Admin);
        var item = await campus.Item(finder);
        await campus.Claim(requester, item, campus.Other);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var row = await db.Items.SingleAsync(f => f.Id == item);
        row.CreatedAt = DateTimeOffset.UtcNow.AddYears(-1);
        row.CustodianId = null;
        row.CustodianDepartmentId = campus.Department;
        await db.SaveChangesAsync();
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")).DateTime).ToString("yyyy-MM-dd");
        var authorized = await CampusFixture.Response(await staff.GetAsync($"/api/v1/statistics?from={day}&to={day}&departmentId={campus.Department}"));
        Assert.Equal(1, authorized.GetProperty("pendingClaims").GetInt32());
        Assert.Equal(0, authorized.GetProperty("availableItems").GetInt32());
        var otherDepartment = await CampusFixture.Response(await admin.GetAsync($"/api/v1/statistics?from={day}&to={day}&departmentId={campus.SecondDepartment}"));
        Assert.Equal(0, otherDepartment.GetProperty("pendingClaims").GetInt32());
    }

    [DatabaseFact]
    public async Task Date_filter_includes_cairo_day_boundaries_and_excludes_the_following_midnight()
    {
        using var admin = await campus.Login(campus.Admin);
        var day = DateOnly.Parse("2026-04-24");
        var start = CairoCalendar.StartOfDay(day);
        var end = CairoCalendar.StartOfDay(day.AddDays(1));
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.Reports.AddRange(
                Report(start.AddMilliseconds(-1), "Outside before Cairo midnight"),
                Report(start, "At Cairo midnight"),
                Report(end.AddMilliseconds(-1), "Before next Cairo midnight"),
                Report(end, "At next Cairo midnight"));
            await db.SaveChangesAsync();
        }

        var response = await CampusFixture.Response(await admin.GetAsync($"/api/v1/statistics?from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}"));
        Assert.Equal(2, response.GetProperty("totalReports").GetInt32());
        var recurring = Assert.Single(response.GetProperty("recurrence").EnumerateArray());
        Assert.Equal("TEST DST boundary", recurring.GetProperty("type").GetString());
        Assert.Equal(2, recurring.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/statistics?from=2026-04-25&to=2026-04-24")).StatusCode);

        SmartCampus.Domain.MaintenanceReport Report(DateTimeOffset createdAt, string description) => new()
        {
            ReporterId = campus.Student,
            DepartmentId = campus.Department,
            FacultyId = campus.Faculty,
            BuildingId = campus.Building,
            FloorId = campus.Floor,
            Description = description,
            Details = "TEST boundary",
            Status = "review",
            FaultType = "TEST DST boundary",
            CreatedAt = createdAt
        };
    }
}
