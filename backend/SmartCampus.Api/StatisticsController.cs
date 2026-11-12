using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

public sealed record StatisticsLocation(Guid Id, string Ar, string En, int Count, double? X, double? Y, int Version);
public sealed record RecurringFault(string Type, string BuildingAr, string BuildingEn, string FloorAr, string FloorEn, int Count);
public sealed record WeeklyReportCount(DateOnly WeekOf, int Count);
public sealed record StatisticsResponse(int TotalReports, Dictionary<string, int> ByStatus, int AvailableItems,
    int PendingClaims, Dictionary<string, int> Locations, Dictionary<string, int> FaultTypes,
    StatisticsLocation[] Buildings, StatisticsLocation[] UnplacedBuildings, RecurringFault[] Recurrence,
    int OpenReports, double ResolutionRate, double? AverageResolutionHours, double? OldestOpenDays,
    WeeklyReportCount[] WeeklyTrend);

[ApiController, Authorize, Route("api/v1/statistics")]
public sealed class StatisticsController(CampusDb db, Access access) : ControllerBase
{
    [HttpGet]
    public async Task<StatisticsResponse> Statistics(DateOnly? from, DateOnly? to, Guid? departmentId)
    {
        var actor = await access.Current();
        if (to == DateOnly.MaxValue || (from.HasValue && to.HasValue && from > to)) throw new RuleException("invalid_date_range");
        if (departmentId.HasValue) Access.Require(actor.Admin || actor.Scopes.Contains(departmentId.Value));
        var start = from.HasValue ? CairoCalendar.StartOfDay(from.Value) : DateTimeOffset.MinValue;
        var end = to.HasValue ? CairoCalendar.StartOfDay(to.Value.AddDays(1)) : DateTimeOffset.MaxValue;
        var reports = db.Reports.AsNoTracking().Where(r => r.CreatedAt >= start && r.CreatedAt < end &&
            (!departmentId.HasValue || r.DepartmentId == departmentId) &&
            (actor.Admin || r.ReporterId == actor.Id || (actor.Roles.Contains("maintenance") && actor.Scopes.Contains(r.DepartmentId))));
        var items = db.Items.AsNoTracking().Where(f => f.CreatedAt >= start && f.CreatedAt < end &&
            (!departmentId.HasValue || f.CustodianDepartmentId == departmentId) &&
            (actor.Admin || f.UploaderId == actor.Id || f.CustodianId == actor.Id ||
                (actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty))));

        var counts = await reports.GroupBy(r => r.CancelledAt.HasValue ? "cancelled" : r.Status)
            .Select(g => new { status = g.Key, count = g.Count() }).ToDictionaryAsync(g => g.status, g => g.count);
        var byStatus = new[] { "submitted", "review", "repair", "resolved", "rejected", "cancelled" }
            .ToDictionary(status => status, status => counts.GetValueOrDefault(status));
        var totalReports = counts.Values.Sum();
        var nonCancelled = totalReports - byStatus["cancelled"];
        var openQuery = reports.Where(r => !r.CancelledAt.HasValue &&
            (r.Status == "submitted" || r.Status == "review" || r.Status == "repair"));
        var openReports = await openQuery.CountAsync();
        var oldestOpen = await openQuery.Select(r => (DateTimeOffset?)r.CreatedAt).MinAsync();
        var resolvedPairs = await reports.Where(r => !r.CancelledAt.HasValue && r.Status == "resolved" && r.ResolvedAt.HasValue)
            .Select(r => new { r.CreatedAt, ResolvedAt = r.ResolvedAt!.Value }).ToArrayAsync();
        var resolutionHours = resolvedPairs.Select(r => (r.ResolvedAt - r.CreatedAt).TotalHours).Where(h => h >= 0).ToArray();

        var locations = await reports.Join(db.Locations.AsNoTracking(), r => r.FacultyId, l => l.Id, (r, l) => l.Ar)
            .GroupBy(name => name).Select(g => new { name = g.Key, count = g.Count() })
            .ToDictionaryAsync(x => x.name, x => x.count);
        var faultTypes = await reports.Where(r => r.FaultType != null).GroupBy(r => r.FaultType!)
            .Select(g => new { type = g.Key, count = g.Count() }).ToDictionaryAsync(g => g.type, g => g.count);
        var buildingGroups = await reports.Join(db.Locations.AsNoTracking(), r => r.BuildingId, l => l.Id, (r, l) => new { Report = r, Building = l })
            .GroupBy(x => new { x.Building.Id, x.Building.Ar, x.Building.En, x.Building.MapX, x.Building.MapY, x.Building.PositionVersion })
            .Select(g => new { g.Key.Id, g.Key.Ar, g.Key.En, g.Key.MapX, g.Key.MapY, g.Key.PositionVersion, Count = g.Count() }).ToArrayAsync();
        var buildingCounts = buildingGroups.ToDictionary(x => x.Id, x => x.Count);
        var buildings = buildingGroups.Select(b => new StatisticsLocation(b.Id, b.Ar, b.En, b.Count, b.MapX, b.MapY, b.PositionVersion)).ToArray();

        var recurrenceRows = await reports.Where(r => r.FaultType != null)
            .Join(db.Locations.AsNoTracking(), r => r.BuildingId, l => l.Id, (r, b) => new { Report = r, Building = b })
            .Join(db.Locations.AsNoTracking(), x => x.Report.FloorId, l => l.Id,
                (x, floor) => new { x.Report.FaultType, x.Building.Ar, x.Building.En, FloorAr = floor.Ar, FloorEn = floor.En })
            .GroupBy(x => new { x.FaultType, x.Ar, x.En, x.FloorAr, x.FloorEn })
            .Select(g => new { g.Key.FaultType, g.Key.Ar, g.Key.En, g.Key.FloorAr, g.Key.FloorEn, Count = g.Count() })
            .Where(x => x.Count > 1).OrderByDescending(x => x.Count).ToArrayAsync();
        var recurrence = recurrenceRows.Select(x => new RecurringFault(x.FaultType!, x.Ar, x.En, x.FloorAr, x.FloorEn, x.Count)).ToArray();

        var locationIds = await reports.Select(r => r.FacultyId).Union(reports.Select(r => r.BuildingId))
            .Union(reports.Select(r => r.FloorId)).Distinct().ToArrayAsync();
        var allBuildings = await db.Locations.AsNoTracking().Where(x => x.Kind == "building" && (actor.Admin || locationIds.Contains(x.Id)))
            .Select(x => new { x.Id, x.Ar, x.En, x.MapX, x.MapY, x.PositionVersion }).ToArrayAsync();
        var map = allBuildings.Select(b => new StatisticsLocation(b.Id, b.Ar, b.En,
            buildingCounts.GetValueOrDefault(b.Id), b.MapX, b.MapY, b.PositionVersion)).ToArray();

        var now = DateTimeOffset.UtcNow;
        var cairoZone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
        var cairoToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, cairoZone).DateTime);
        var currentWeek = cairoToday.AddDays(-(((int)cairoToday.DayOfWeek + 6) % 7));
        var trend = new List<WeeklyReportCount>(8);
        for (var offset = 7; offset >= 0; offset--)
        {
            var week = currentWeek.AddDays(-7 * offset);
            var weekEnd = week.AddDays(7);
            var weekCount = await reports.CountAsync(r => r.CreatedAt >= CairoCalendar.StartOfDay(week) && r.CreatedAt < CairoCalendar.StartOfDay(weekEnd));
            trend.Add(new WeeklyReportCount(week, weekCount));
        }

        var pendingClaims = await db.Claims.AsNoTracking().CountAsync(c => c.Status == "pending" && c.CreatedAt >= start && c.CreatedAt < end &&
            db.Items.Any(f => f.Id == c.ItemId && (!departmentId.HasValue || f.CustodianDepartmentId == departmentId) &&
                (actor.Admin || c.RequesterId == actor.Id || f.CustodianId == actor.Id ||
                    (actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty)))));
        var mapped = map.Where(x => x.X.HasValue && x.Y.HasValue).ToArray();
        var unplaced = map.Where(x => !x.X.HasValue || !x.Y.HasValue).ToArray();

        return new StatisticsResponse(totalReports, byStatus, await items.CountAsync(f => f.State == "available"), pendingClaims,
            locations, faultTypes, mapped, unplaced, recurrence, openReports,
            nonCancelled == 0 ? 0 : (double)byStatus["resolved"] / nonCancelled,
            resolutionHours.Length == 0 ? null : resolutionHours.Average(),
            oldestOpen is null ? null : (now - oldestOpen.Value).TotalDays, trend.ToArray());
    }
}
