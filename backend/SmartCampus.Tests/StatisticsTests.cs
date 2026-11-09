using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class StatisticsTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
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
}
