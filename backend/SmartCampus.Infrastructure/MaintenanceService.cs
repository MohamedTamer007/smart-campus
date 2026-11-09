using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed record MaintenanceInput(string Description, Guid Faculty, Guid Building, Guid Floor, string Details);
public sealed record DecisionInput(string Type, string Priority, string Risk);
public sealed record MaintenanceUpdate(int Version, string Status, string? Assignee, string Note, DecisionInput? Decision);

public sealed class MaintenanceService(CampusDb db, Access access, Events events, PrivateMedia media)
{
    public async Task<object> Create(MaintenanceInput input, IFormFile? photo)
    {
        var actor = await access.Current();
        var report = new MaintenanceReport { ReporterId = actor.Id };
        await Apply(report, input);
        MediaObject? attachment = null;
        if (photo is not null) attachment = await media.Upload(photo, new() { ReportId = report.Id, Bucket = "maintenance-images" });
        try
        {
            db.Reports.Add(report);
            if (attachment is not null) db.Media.Add(attachment);
            History(actor, report, "Submitted");
            Queue(report);
            events.Notify([actor.Id], "submitted", "maintenance", report.Id);
            events.Audit(actor, "MaintenanceCreated", report.Id);
            await db.SaveChangesAsync();
        }
        catch { if (attachment is not null) await media.Delete(attachment); throw; }
        return await Detail(report.Id);
    }

    public async Task<object[]> List(int page, int pageSize)
    {
        var actor = await access.Current();
        var query = db.Reports.Where(r => actor.Admin || r.ReporterId == actor.Id ||
            (actor.Roles.Contains("maintenance") && actor.Scopes.Contains(r.DepartmentId)));
        var ids = await query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(r => r.Id).ToArrayAsync();
        var reports = new List<object>();
        foreach (var id in ids) reports.Add(await Detail(id));
        return reports.ToArray();
    }

    public async Task<object> Detail(Guid id)
    {
        var actor = await access.Current();
        var report = await db.Reports.FindAsync(id) ?? throw new RuleException("not_found", 404);
        var staff = actor.Admin || actor.Maintenance(report.DepartmentId);
        Access.Require(staff || report.ReporterId == actor.Id);
        var history = await db.History.Where(h => h.ReportId == id).OrderBy(h => h.CreatedAt)
            .Select(h => new { at = h.CreatedAt, actor = h.ActorId, status = h.Status, note = h.Note, assignee = h.AssigneeId }).ToArrayAsync();
        var assessment = staff ? await db.Assessments.Where(a => a.ReportId == id && a.InputVersion == report.ContentVersion)
            .Select(a => new { state = a.State, type = a.FaultType, priority = a.Priority, risk = a.Risk }).FirstOrDefaultAsync() : null;
        var attachment = await db.Media.Where(m => m.ReportId == id).Select(m => (Guid?)m.Id).FirstOrDefaultAsync();
        var locationRecords = await db.Locations.Where(l => l.Id == report.FacultyId || l.Id == report.BuildingId || l.Id == report.FloorId).ToDictionaryAsync(l => l.Id);
        var allowedAssignees = staff ? await db.Users.Where(u => u.Active && u.EmailConfirmed &&
            db.Scopes.Any(s => s.UserId == u.Id && s.DepartmentId == report.DepartmentId) &&
            db.UserRoles.Any(r => r.UserId == u.Id && db.Roles.Any(role => role.Id == r.RoleId && role.Name == "maintenance")))
            .Select(u => new { u.Id, name = u.DisplayName }).ToArrayAsync() : [];
        return new { report.Id, report.Reference, ownerId = report.ReporterId, report.Description, faculty = report.FacultyId,
            building = report.BuildingId, floor = report.FloorId, report.Details, report.Status, report.CancelledAt,
            locationNames = new { faculty = new { locationRecords[report.FacultyId].Ar, locationRecords[report.FacultyId].En },
                building = new { locationRecords[report.BuildingId].Ar, locationRecords[report.BuildingId].En },
                floor = new { locationRecords[report.FloorId].Ar, locationRecords[report.FloorId].En } },
            report.Version, report.CreatedAt, departmentId = report.DepartmentId, assignee = report.AssigneeId, allowedAssignees, history, assessment,
            decision = staff ? new { type = report.FaultType, priority = report.Priority, risk = report.Risk } : null,
            mediaUrl = attachment.HasValue ? $"/api/v1/media/{attachment}" : null,
            canEdit = report.ReporterId == actor.Id && report.Status == "submitted" && report.CancelledAt is null,
            canViewAssessment = staff, canUpdate = staff && report.CancelledAt is null && report.Status is not ("resolved" or "rejected") };
    }

    public async Task<object> Edit(Guid id, int version, MaintenanceInput input, IFormFile? photo)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var report = await Locked(id);
        OwnerEditable(actor, report, version);
        await Apply(report, input);
        MediaObject? attachment = null;
        var oldAttachments = await db.Media.Where(m => m.ReportId == id).ToArrayAsync();
        if (photo is not null)
        {
            attachment = await media.Upload(photo, new() { ReportId = id, Bucket = "maintenance-images" });
            db.Media.RemoveRange(oldAttachments);
            db.Media.Add(attachment);
        }
        try
        {
            report.Version++;
            report.ContentVersion++;
            History(actor, report, "Edited");
            Queue(report);
            events.Audit(actor, "MaintenanceEdited", id);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch { if (attachment is not null) await media.Delete(attachment); throw; }
        if (attachment is not null) foreach (var old in oldAttachments) await media.Delete(old);
        return await Detail(id);
    }

    public async Task<object> Cancel(Guid id, int version)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var report = await Locked(id);
        OwnerEditable(actor, report, version);
        report.CancelledAt = DateTimeOffset.UtcNow;
        report.Version++;
        History(actor, report, "Cancelled before handling");
        events.Audit(actor, "MaintenanceCancelled", id);
        events.Notify([actor.Id], "cancelled", "maintenance", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await Detail(id);
    }

    public async Task<object> Update(Guid id, MaintenanceUpdate input)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var report = await Locked(id);
        Access.Require(actor.Admin || actor.Maintenance(report.DepartmentId));
        Rules.Version(report.Version, input.Version);
        if (report.CancelledAt.HasValue) throw new RuleException("invalid_transition", 409);
        Rules.Transition(report.Status, input.Status);
        if (input.Status == "rejected") Rules.Required(input.Note);
        if (!string.IsNullOrEmpty(input.Assignee))
        {
            if (!Guid.TryParse(input.Assignee, out var assignee)) throw new RuleException("invalid_assignee");
            var roleId = await db.Roles.Where(r => r.Name == "maintenance").Select(r => r.Id).FirstOrDefaultAsync();
            Access.Require(await db.Scopes.AnyAsync(s => s.UserId == assignee && s.DepartmentId == report.DepartmentId) &&
                await db.UserRoles.AnyAsync(r => r.UserId == assignee && r.RoleId == roleId) &&
                await db.Users.AnyAsync(u => u.Id == assignee && u.Active && u.EmailConfirmed));
            report.AssigneeId = assignee;
        }
        if (input.Status == "repair" && report.AssigneeId is null) throw new RuleException("assignee_required");
        report.Status = input.Status;
        report.Version++;
        if (input.Decision is not null)
        {
            report.FaultType = Rules.Required(input.Decision.Type, 100);
            report.Priority = Rules.Required(input.Decision.Priority, 50);
            report.Risk = Rules.Required(input.Decision.Risk, 50);
        }
        if (report.Status == "resolved") report.ResolvedAt = DateTimeOffset.UtcNow;
        History(actor, report, input.Note);
        events.Audit(actor, "MaintenanceUpdated", id);
        events.Notify([report.ReporterId], report.Status, "maintenance", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await Detail(id);
    }

    private async Task Apply(MaintenanceReport report, MaintenanceInput input)
    {
        report.Description = Rules.Required(input.Description);
        report.Details = Rules.Required(input.Details, 1000);
        var faculty = await db.Locations.FindAsync(input.Faculty);
        var building = await db.Locations.FindAsync(input.Building);
        var floor = await db.Locations.FindAsync(input.Floor);
        if (faculty?.Kind != "faculty" || building?.Kind != "building" || floor?.Kind != "floor" ||
            building.ParentId != faculty.Id || floor.ParentId != building.Id) throw new RuleException("invalid_location");
        var department = floor.MaintenanceDepartmentId ?? building.MaintenanceDepartmentId ?? faculty.MaintenanceDepartmentId;
        if (department is null || !await db.Departments.AnyAsync(d => d.Id == department && d.Active && d.Maintenance))
            throw new RuleException("location_unavailable", 503);
        report.FacultyId = faculty.Id;
        report.BuildingId = building.Id;
        report.FloorId = floor.Id;
        report.DepartmentId = department.Value;
    }

    private async Task<MaintenanceReport> Locked(Guid id) => await db.Reports.FromSqlInterpolated(
        $"SELECT * FROM smart_campus.\"Reports\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
        ?? throw new RuleException("not_found", 404);

    private static void OwnerEditable(Actor actor, MaintenanceReport report, int version)
    {
        Access.Require(report.ReporterId == actor.Id);
        Rules.Version(report.Version, version);
        if (report.Status != "submitted" || report.CancelledAt.HasValue) throw new RuleException("invalid_transition", 409);
    }

    private void History(Actor actor, MaintenanceReport report, string note) => db.History.Add(new() {
        ReportId = report.Id, ActorId = actor.Id, Status = report.Status, Note = note, AssigneeId = report.AssigneeId,
        DecisionJson = System.Text.Json.JsonSerializer.Serialize(new { report.FaultType, report.Priority, report.Risk }) });

    private void Queue(MaintenanceReport report)
    {
        db.Assessments.Add(new() { ReportId = report.Id, InputVersion = report.ContentVersion });
        db.Jobs.Add(new() { Kind = "maintenance", EntityId = report.Id, InputVersion = report.ContentVersion, Key = $"maintenance:{report.Id}:{report.ContentVersion}" });
    }
}
