using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

[ApiController, Authorize, Route("api/v1/statistics")]
public sealed class StatisticsController(CampusDb db, Access access) : ControllerBase
{
    [HttpGet]
    public async Task<object> Statistics(DateOnly? from, DateOnly? to, Guid? departmentId)
    {
        var actor = await access.Current();
        if (to == DateOnly.MaxValue || (from.HasValue && to.HasValue && from > to)) throw new SmartCampus.Domain.RuleException("invalid_date_range");
        if (departmentId.HasValue) Access.Require(actor.Admin || actor.Scopes.Contains(departmentId.Value));
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var start = from.HasValue ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(from.Value.ToDateTime(TimeOnly.MinValue), zone)) : DateTimeOffset.MinValue;
        var end = to.HasValue ? new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), zone)) : DateTimeOffset.MaxValue;
        var reports = db.Reports.Where(r => r.CreatedAt >= start && r.CreatedAt < end &&
            (!departmentId.HasValue || r.DepartmentId == departmentId) && (actor.Admin || r.ReporterId == actor.Id || (actor.Roles.Contains("maintenance") && actor.Scopes.Contains(r.DepartmentId))));
        var items = db.Items.Where(f => f.CreatedAt >= start && f.CreatedAt < end && (!departmentId.HasValue || f.CustodianDepartmentId == departmentId) &&
            (actor.Admin || f.UploaderId == actor.Id || f.CustodianId == actor.Id || (actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty))));
        var reportRows = await reports.Select(r => new { r.Status, r.CancelledAt, r.FacultyId, r.FaultType, r.FloorId }).ToArrayAsync();
        var byStatus = new Dictionary<string, int>();
        foreach (var status in new[] { "submitted", "review", "repair", "resolved", "rejected" }) byStatus[status] = reportRows.Count(r => r.Status == status && r.CancelledAt is null);
        byStatus["cancelled"] = reportRows.Count(r => r.CancelledAt.HasValue);
        return new { totalReports = reportRows.Length, byStatus, availableItems = await items.CountAsync(f => f.State == "available"),
            pendingClaims = await db.Claims.CountAsync(c => c.Status == "pending" && c.CreatedAt >= start && c.CreatedAt < end &&
                (actor.Admin || c.RequesterId == actor.Id || items.Any(f => f.Id == c.ItemId && (f.CustodianId == actor.Id || (actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty)))))),
            locations = reportRows.GroupBy(r => r.FacultyId.ToString()).ToDictionary(g => g.Key, g => g.Count()),
            faultTypes = reportRows.Where(r => r.FaultType is not null).GroupBy(r => r.FaultType!).ToDictionary(g => g.Key, g => g.Count()),
            recurrence = reportRows.Where(r => r.FaultType is not null).GroupBy(r => new { r.FaultType, r.FloorId }).Where(g => g.Count() > 1).Select(g => new { g.Key.FaultType, g.Key.FloorId, count = g.Count() }) };
    }
}
