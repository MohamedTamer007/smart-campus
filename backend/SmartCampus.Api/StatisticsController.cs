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
        var start = from.HasValue ? CairoCalendar.StartOfDay(from.Value) : DateTimeOffset.MinValue;
        var end = to.HasValue ? CairoCalendar.StartOfDay(to.Value.AddDays(1)) : DateTimeOffset.MaxValue;
        var reports = db.Reports.Where(r => r.CreatedAt >= start && r.CreatedAt < end &&
            (!departmentId.HasValue || r.DepartmentId == departmentId) && (actor.Admin || r.ReporterId == actor.Id || (actor.Roles.Contains("maintenance") && actor.Scopes.Contains(r.DepartmentId))));
        var items = db.Items.Where(f => f.CreatedAt >= start && f.CreatedAt < end && (!departmentId.HasValue || f.CustodianDepartmentId == departmentId) &&
            (actor.Admin || f.UploaderId == actor.Id || f.CustodianId == actor.Id || (actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty))));
        var counts = await reports.GroupBy(r => r.CancelledAt.HasValue ? "cancelled" : r.Status).Select(g => new { status = g.Key, count = g.Count() }).ToDictionaryAsync(g => g.status, g => g.count);
        var byStatus = new[] { "submitted", "review", "repair", "resolved", "rejected", "cancelled" }.ToDictionary(status => status, status => counts.GetValueOrDefault(status));
        return new { totalReports = counts.Values.Sum(), byStatus, availableItems = await items.CountAsync(f => f.State == "available"),
            pendingClaims = await db.Claims.CountAsync(c => c.Status == "pending" && c.CreatedAt >= start && c.CreatedAt < end &&
                db.Items.Any(f => f.Id == c.ItemId && (!departmentId.HasValue || f.CustodianDepartmentId == departmentId) &&
                    (actor.Admin || c.RequesterId == actor.Id || f.CustodianId == actor.Id || (actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty))))),
            locations = await reports.GroupBy(r => r.FacultyId).Select(g => new { id = g.Key, count = g.Count() }).ToDictionaryAsync(g => g.id.ToString(), g => g.count),
            faultTypes = await reports.Where(r => r.FaultType != null).GroupBy(r => r.FaultType!).Select(g => new { type = g.Key, count = g.Count() }).ToDictionaryAsync(g => g.type, g => g.count),
            recurrence = await reports.Where(r => r.FaultType != null).GroupBy(r => new { r.FaultType, r.FloorId }).Where(g => g.Count() > 1).Select(g => new { g.Key.FaultType, g.Key.FloorId, count = g.Count() }).ToArrayAsync() };
    }
}
