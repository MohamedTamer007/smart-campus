using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed record MaintenanceInput(string Description, Guid Faculty, Guid Building, Guid Floor, string Details);
public sealed record DecisionInput(string Type, string Priority, string Risk);
public sealed record MaintenanceUpdate(int Version, string Status, string? Assignee, string Note, DecisionInput? Decision);
public sealed record AiReviewInput(int Version, bool ConfirmedNonSensitive, bool IncludeImage);

public sealed class MaintenanceService(CampusDb db, Access access, Events events, PrivateMedia media, IConfiguration config)
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
        var reports = await query.AsNoTracking().OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return await Responses(reports, actor);
    }

    public async Task<object> Detail(Guid id)
    {
        var actor = await access.Current();
        var report = await db.Reports.FindAsync(id) ?? throw new RuleException("not_found", 404);
        return (await Responses([report], actor))[0];
    }

    private sealed record Assignee(Guid DepartmentId, Guid Id, string Name);
    private sealed record Attachment(Guid ReportId, Guid Id);
    private sealed record ResponseData(ILookup<Guid, MaintenanceHistory> History, Dictionary<Guid, Assessment> Assessments,
        ILookup<Guid, Attachment> Media, Dictionary<Guid, CampusLocation> Locations, ILookup<Guid, Assignee> Assignees);

    private async Task<object[]> Responses(MaintenanceReport[] reports, Actor actor)
    {
        if (reports.Length == 0) return [];
        foreach (var report in reports) Access.Require(actor.Admin || actor.Maintenance(report.DepartmentId) || report.ReporterId == actor.Id);
        var related = await Related(reports, actor);
        return reports.Select(report => Response(report, actor, related)).ToArray();
    }

    private async Task<ResponseData> Related(MaintenanceReport[] reports, Actor actor)
    {
        var ids = reports.Select(r => r.Id).ToArray();
        var staffReports = reports.Where(r => actor.Admin || actor.Maintenance(r.DepartmentId)).ToArray();
        var history = await db.History.AsNoTracking().Where(h => ids.Contains(h.ReportId)).OrderBy(h => h.CreatedAt).ToArrayAsync();
        var attachments = await db.Media.Where(m => m.ReportId.HasValue && ids.Contains(m.ReportId.Value))
            .Select(m => new Attachment(m.ReportId!.Value, m.Id)).ToArrayAsync();
        var locationIds = reports.SelectMany(r => new[] { r.FacultyId, r.BuildingId, r.FloorId }).Distinct().ToArray();
        var locations = await db.Locations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToDictionaryAsync(l => l.Id);
        return new(history.ToLookup(h => h.ReportId), await CurrentAssessments(staffReports), attachments.ToLookup(m => m.ReportId),
            locations, (await Assignees(staffReports.Select(r => r.DepartmentId).Distinct().ToArray())).ToLookup(a => a.DepartmentId));
    }

    private async Task<Dictionary<Guid, Assessment>> CurrentAssessments(MaintenanceReport[] reports)
    {
        if (reports.Length == 0) return [];
        var versions = reports.ToDictionary(r => r.Id, r => r.ContentVersion);
        var ids = versions.Keys.ToArray();
        var inputVersions = versions.Values.Distinct().ToArray();
        var assessments = await db.Assessments.AsNoTracking().Where(a => ids.Contains(a.ReportId) && inputVersions.Contains(a.InputVersion)).ToArrayAsync();
        return assessments.Where(a => versions[a.ReportId] == a.InputVersion).ToDictionary(a => a.ReportId);
    }

    private async Task<Assignee[]> Assignees(Guid[] departments)
    {
        if (departments.Length == 0) return [];
        return await (from scope in db.Scopes
            join user in db.Users on scope.UserId equals user.Id
            where departments.Contains(scope.DepartmentId) && user.Active && user.EmailConfirmed &&
                db.UserRoles.Any(r => r.UserId == user.Id && db.Roles.Any(role => role.Id == r.RoleId && role.Name == "maintenance"))
            select new Assignee(scope.DepartmentId, user.Id, user.DisplayName)).Distinct().ToArrayAsync();
    }

    private object Response(MaintenanceReport report, Actor actor, ResponseData related)
    {
        var staff = actor.Admin || actor.Maintenance(report.DepartmentId);
        related.Assessments.TryGetValue(report.Id, out var assessmentRecord);
        var assessment = assessmentRecord is null ? null : new { state = assessmentRecord.State, type = assessmentRecord.FaultType,
            priority = assessmentRecord.Priority, risk = assessmentRecord.Risk, inputReviewed = assessmentRecord.ReviewedAt.HasValue };
        var attachment = related.Media[report.Id].Select(m => (Guid?)m.Id).FirstOrDefault();
        var locationRecords = related.Locations;
        var allowedAssignees = staff ? related.Assignees[report.DepartmentId].Select(a => new { a.Id, name = a.Name }).ToArray() : [];
        var history = related.History[report.Id].Select(h => new { at = h.CreatedAt, actor = h.ActorId, status = h.Status, note = h.Note, assignee = h.AssigneeId }).ToArray();
        return new { report.Id, report.Reference, ownerId = report.ReporterId, report.Description, faculty = report.FacultyId,
            building = report.BuildingId, floor = report.FloorId, report.Details, report.Status, report.CancelledAt,
            locationNames = new { faculty = new { locationRecords[report.FacultyId].Ar, locationRecords[report.FacultyId].En },
                building = new { locationRecords[report.BuildingId].Ar, locationRecords[report.BuildingId].En },
                floor = new { locationRecords[report.FloorId].Ar, locationRecords[report.FloorId].En } },
            report.Version, report.CreatedAt, departmentId = report.DepartmentId, assignee = report.AssigneeId, allowedAssignees, history, assessment,
            decision = staff ? new { type = report.FaultType, priority = report.Priority, risk = report.Risk } : null,
            mediaUrl = attachment.HasValue ? $"/api/v1/media/{attachment}" : null,
            canEdit = report.ReporterId == actor.Id && report.Status == "submitted" && report.CancelledAt is null,
            canViewAssessment = staff, canUpdate = staff && report.CancelledAt is null && report.Status is not ("resolved" or "rejected"),
            canReviewAiInput = staff && report.CancelledAt is null && (report.Status is "submitted" or "review") &&
                assessmentRecord is { ReviewedAt: null } && assessmentRecord.State != "completed" && AiProviders.Configured(config),
            canReviewAiImage = staff && AiProviders.CanReviewImage(config) && attachment.HasValue };
    }

    public async Task<object> ReviewAi(Guid id, AiReviewInput input)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var report = await Locked(id);
        Access.Require(actor.Admin || actor.Maintenance(report.DepartmentId));
        Rules.Version(report.Version, input.Version);
        if (!input.ConfirmedNonSensitive) throw new RuleException("ai_review_required");
        if (report.CancelledAt.HasValue || report.Status is not ("submitted" or "review")) throw new RuleException("invalid_transition", 409);
        if (!AiProviders.Configured(config)) throw new RuleException("ai_not_configured", 503);
        var assessment = await db.Assessments.SingleAsync(a => a.ReportId == id && a.InputVersion == report.ContentVersion);
        if (assessment.ReviewedAt.HasValue || assessment.State == "completed") throw new RuleException("conflict", 409);
        var jobKey = $"maintenance:{id}:{report.ContentVersion}";
        var job = await db.Jobs.FromSqlInterpolated($"SELECT * FROM smart_campus.\"Jobs\" WHERE \"Key\"={jobKey} FOR UPDATE").SingleAsync();
        if (job.State == "running") throw new RuleException("conflict", 409);
        if (input.IncludeImage)
        {
            if (!AiProviders.CanReviewImage(config))
                throw new RuleException("ai_images_not_permitted", 503);
            assessment.ReviewedImageId = await db.Media.Where(m => m.ReportId == id).Select(m => (Guid?)m.Id).SingleOrDefaultAsync()
                ?? throw new RuleException("invalid_upload");
        }
        assessment.ReviewedBy = actor.Id;
        assessment.ReviewedAt = DateTimeOffset.UtcNow;
        assessment.State = "pending";
        assessment.FailureCode = null;
        job.State = "pending";
        job.Attempts = 0;
        job.AvailableAt = DateTimeOffset.UtcNow;
        report.Version++;
        events.Audit(actor, "MaintenanceAiInputReviewed", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await Detail(id);
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
