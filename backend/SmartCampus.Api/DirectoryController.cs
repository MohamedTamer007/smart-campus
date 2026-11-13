using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

[ApiController, Authorize, Route("api/v1/directory")]
public sealed class DirectoryController(CampusDb db, Access access) : ControllerBase
{
    [HttpGet("locations")]
    public async Task<object> Locations()
    {
        await access.Current();
        var locations = await db.Locations.AsNoTracking().ToArrayAsync();
        return BuildLocationTree(locations);
    }

    public static IReadOnlyList<FacultyLocationEntry> BuildLocationTree(IReadOnlyCollection<CampusLocation> locations)
    {
        var childrenByParent = locations.Where(location => location.ParentId.HasValue)
            .GroupBy(location => location.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        CampusLocation[] Children(Guid parentId, string kind) => childrenByParent.TryGetValue(parentId, out var children)
            ? children.Where(child => child.Kind == kind).ToArray()
            : [];

        return locations.Where(location => location.Kind == "faculty").Select(faculty => new FacultyLocationEntry(faculty.Id, faculty.Ar, faculty.En,
            Children(faculty.Id, "building").Select(building => new BuildingLocationEntry(building.Id, building.Ar, building.En,
                Children(building.Id, "floor").Select(floor => new FloorLocationEntry(floor.Id, floor.Ar, floor.En)).ToArray())).ToArray())).ToArray();
    }

    [HttpGet("departments")]
    public async Task<object> Departments()
    {
        await access.Current();
        return await db.Departments.Where(d => d.Active).Select(d => new { d.Id, d.Code, d.Ar, d.En, d.CanReceive, d.Maintenance }).ToArrayAsync();
    }

    [HttpGet("assignees/{departmentId:guid}")]
    public async Task<object> Assignees(Guid departmentId)
    {
        var actor = await access.Current();
        Access.Require(actor.Admin || actor.Maintenance(departmentId));
        return await db.Users.Where(u => u.Active && u.EmailConfirmed && db.Scopes.Any(s => s.UserId == u.Id && s.DepartmentId == departmentId) &&
            db.UserRoles.Any(r => r.UserId == u.Id && db.Roles.Any(role => role.Id == r.RoleId && role.Name == "maintenance")))
            .Select(u => new { u.Id, name = u.DisplayName }).ToArrayAsync();
    }
}

public sealed record FacultyLocationEntry(Guid Id, string Ar, string En, IReadOnlyList<BuildingLocationEntry> Buildings);
public sealed record BuildingLocationEntry(Guid Id, string Ar, string En, IReadOnlyList<FloorLocationEntry> Floors);
public sealed record FloorLocationEntry(Guid Id, string Ar, string En);

[ApiController, Authorize, Route("api/v1/media")]
public sealed class MediaController(CampusDb db, Access access, PrivateMedia media) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Download(Guid id)
    {
        var actor = await access.Current();
        var metadata = await db.Media.FindAsync(id) ?? throw new RuleException("not_found", 404);
        if (metadata.ReportId.HasValue)
        {
            var report = (await db.Reports.FindAsync(metadata.ReportId))!;
            Access.Require(actor.Id == report.ReporterId || actor.Admin || actor.Maintenance(report.DepartmentId));
        }
        else Access.Require(await access.ItemReadable(actor, (await db.Items.FindAsync(metadata.ItemId))!));
        return File(await media.Read(metadata), "image/jpeg");
    }
}

[ApiController, Authorize, Route("api/v1/notifications")]
public sealed class NotificationsController(CampusDb db, Access access) : ControllerBase
{
    [HttpGet]
    public async Task<object> List(int page = 1, int pageSize = 50)
    {
        var actor = await access.Current();
        var (offset, size) = Rules.PageWindow(page, pageSize);
        return new { items = await db.Notifications.Where(n => n.RecipientId == actor.Id).OrderByDescending(n => n.CreatedAt).ThenBy(n => n.Id)
            .Skip(offset).Take(size)
            .Select(n => new { n.Id, accountId = n.RecipientId, kind = n.Kind, n.EntityType, n.EntityId, read = n.ReadAt.HasValue,
                to = n.EntityType == "maintenance" ? "/maintenance/" + n.EntityId : n.EntityType == "claim" ? "/claims/" + n.EntityId : "/found/" + n.EntityId }).ToArrayAsync() };
    }
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        var actor = await access.Current();
        var notification = await db.Notifications.FindAsync(id) ?? throw new RuleException("not_found", 404);
        Access.Require(notification.RecipientId == actor.Id);
        notification.ReadAt ??= DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

public sealed record GrantInput(string[] Roles, Guid[] DepartmentIds, string Version);
public sealed record DepartmentInput(string Code, string Ar, string En, bool CanReceive, bool Maintenance);
public sealed record LocationInput(Guid? ParentId, string Kind, string Ar, string En, Guid? MaintenanceDepartmentId);
public sealed record LocationPositionInput(double? X, double? Y, int Version);

[ApiController, Authorize, Route("api/v1/admin")]
public sealed class AdministrationController(CampusDb db, Access access, UserManager<CampusUser> users,
    RoleManager<IdentityRole<Guid>> roles, Events events) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<object> Accounts(int page = 1, int pageSize = 50)
    {
        Access.Require((await access.Current()).Admin);
        var (offset, size) = Rules.PageWindow(page, pageSize);
        var accounts = await db.Users.AsNoTracking().Where(u => u.Active).OrderBy(u => u.Email)
            .Skip(offset).Take(size).ToArrayAsync();
        if (accounts.Length == 0) return new { items = Array.Empty<object>() };
        var userIds = accounts.Select(user => user.Id).ToArray();
        var roleRows = await (from membership in db.UserRoles
            join role in db.Roles on membership.RoleId equals role.Id
            where userIds.Contains(membership.UserId) && role.Name != null
            select new { membership.UserId, Role = role.Name! }).ToArrayAsync();
        var rolesByUser = roleRows.GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Role).OrderBy(role => role, StringComparer.Ordinal).ToArray());
        var scopeRows = await db.Scopes.Where(scope => userIds.Contains(scope.UserId))
            .Join(db.Departments, scope => scope.DepartmentId, department => department.Id,
                (scope, department) => new { scope.UserId, Department = new { department.Id, department.Code } })
            .ToArrayAsync();
        var scopesByUser = scopeRows.GroupBy(row => row.UserId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Department).OrderBy(department => department.Code, StringComparer.Ordinal).ToArray());
        var demoUsers = await db.UserClaims.AsNoTracking().Where(claim => userIds.Contains(claim.UserId) &&
                claim.ClaimType == DemoPreparationService.DemoMarkerClaimType && claim.ClaimValue == "true")
            .Select(claim => claim.UserId).Distinct().ToHashSetAsync();
        var entries = accounts.Select(user =>
        {
            var grants = rolesByUser.GetValueOrDefault(user.Id) ?? [];
            var scopes = scopesByUser.GetValueOrDefault(user.Id) ?? [];
            return (object)new { key = user.Id, version = user.ConcurrencyStamp, account = new { id = user.Id, name = user.DisplayName, user.Email,
                role = !user.EmailConfirmed ? "unverified" : grants.Contains("admin") ? "admin" : grants.Contains("maintenance") ? "maintenance" : grants.Contains("department") ? "department" : "user",
                roles = grants, departments = scopes, department = scopes.FirstOrDefault()?.Code, isDemo = demoUsers.Contains(user.Id) } };
        }).ToArray();
        return new { items = entries };
    }

    [HttpPut("accounts/{id:guid}/grants")]
    public async Task<IActionResult> Grants(Guid id, GrantInput input)
    {
        var actor = await access.Current();
        Access.Require(actor.Admin);
        if (input.Roles.Except(["user", "maintenance", "department", "admin"]).Any()) throw new RuleException("invalid_role");
        if (id == actor.Id) throw new RuleException("self_grant_change_forbidden", 403);
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(762342101)");
        Access.Require(await db.UserRoles.AnyAsync(r => r.UserId == actor.Id && db.Roles.Any(role => role.Id == r.RoleId && role.Name == "admin")));
        var user = await db.Users.FromSqlInterpolated($"SELECT * FROM smart_campus.\"AspNetUsers\" WHERE \"Id\"={id} FOR UPDATE").SingleOrDefaultAsync()
            ?? throw new RuleException("not_found", 404);
        if (!user.Active) throw new RuleException("not_found", 404);
        if (user.ConcurrencyStamp != input.Version) throw new RuleException("conflict", 409);
        if (!user.EmailConfirmed) throw new RuleException("unverified", 409);
        if (await db.Departments.CountAsync(d => input.DepartmentIds.Contains(d.Id) && d.Active) != input.DepartmentIds.Distinct().Count()) throw new RuleException("invalid_department");
        foreach (var role in input.Roles.Distinct())
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new(role)));
        var current = await users.GetRolesAsync(user);
        Ensure(await users.RemoveFromRolesAsync(user, current.Except(input.Roles)));
        Ensure(await users.AddToRolesAsync(user, input.Roles.Except(current)));
        db.Scopes.RemoveRange(await db.Scopes.Where(s => s.UserId == id).ToArrayAsync());
        foreach (var department in input.DepartmentIds.Distinct()) db.Scopes.Add(new() { UserId = id, DepartmentId = department, GrantedBy = actor.Id });
        Ensure(await users.UpdateSecurityStampAsync(user));
        await db.Sessions.Where(s => s.UserId == id).ExecuteDeleteAsync();
        events.Audit(actor, "AccountGrantsChanged", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return NoContent();
    }

    [HttpPost("departments")]
    public async Task<object> Department(DepartmentInput input)
    {
        var actor = await access.Current(); Access.Require(actor.Admin);
        var department = new Department { Code = Rules.Required(input.Code, 50), Ar = Rules.Required(input.Ar, 150),
            En = Rules.Required(input.En, 150), CanReceive = input.CanReceive, Maintenance = input.Maintenance };
        db.Departments.Add(department); events.Audit(actor, "DepartmentCreated", department.Id);
        await db.SaveChangesAsync(); return new { department.Id };
    }

    [HttpPost("locations")]
    public async Task<object> Location(LocationInput input)
    {
        var actor = await access.Current(); Access.Require(actor.Admin);
        var parent = input.ParentId.HasValue ? await db.Locations.FindAsync(input.ParentId) : null;
        if ((input.Kind == "faculty" && input.ParentId is not null) || (input.Kind == "building" && parent?.Kind != "faculty") ||
            (input.Kind == "floor" && parent?.Kind != "building") || input.Kind is not ("faculty" or "building" or "floor")) throw new RuleException("invalid_location");
        if (input.MaintenanceDepartmentId.HasValue && !await db.Departments.AnyAsync(d => d.Id == input.MaintenanceDepartmentId && d.Active && d.Maintenance))
            throw new RuleException("invalid_department");
        var location = new CampusLocation { ParentId = input.ParentId, Kind = input.Kind, Ar = Rules.Required(input.Ar, 150), En = Rules.Required(input.En, 150), MaintenanceDepartmentId = input.MaintenanceDepartmentId };
        db.Locations.Add(location); events.Audit(actor, "LocationCreated", location.Id); await db.SaveChangesAsync(); return new { location.Id };
    }

    [HttpPut("locations/{id:guid}/position")]
    public async Task<IActionResult> LocationPosition(Guid id, LocationPositionInput input)
    {
        var actor = await access.Current(); Access.Require(actor.Admin);
        if ((input.X is null) != (input.Y is null) || input.X is < 0 or > 100 || input.Y is < 0 or > 100)
            throw new RuleException("invalid_location_position");
        var location = await db.Locations.SingleOrDefaultAsync(x => x.Id == id && x.Kind == "building")
            ?? throw new RuleException("not_found", 404);
        if (location.PositionVersion != input.Version) throw new RuleException("conflict", 409);
        location.MapX = input.X; location.MapY = input.Y; location.PositionVersion++;
        db.Audit.Add(new() { ActorId = actor.Id, Action = "LocationPositionUpdated", EntityId = id });
        await db.SaveChangesAsync();
        return Ok(new { version = location.PositionVersion });
    }

    private static void Ensure(IdentityResult outcome)
    {
        if (!outcome.Succeeded) throw new RuleException("conflict", 409);
    }
}
