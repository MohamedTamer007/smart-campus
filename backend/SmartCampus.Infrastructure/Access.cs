using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed record Actor(Guid Id, string Name, string Email, string[] Roles, Guid[] Scopes)
{
    public bool Admin => Roles.Contains("admin");
    public bool Maintenance(Guid department) => Roles.Contains("maintenance") && Scopes.Contains(department);
    public bool Department(Guid? department) => department.HasValue && Roles.Contains("department") && Scopes.Contains(department.Value);
    public bool Custodian(FoundItem found) => found.CustodianId == Id || Department(found.CustodianDepartmentId);
}

public sealed class Access(CampusDb db, IHttpContextAccessor http, UserManager<CampusUser> users)
{
    private Actor? cached;
    public async Task<Actor> Current()
    {
        if (cached is not null) return cached;
        if (!Guid.TryParse(http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            throw new RuleException("expired", 401);
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null || !user.Active) throw new RuleException("expired", 401);
        if (!user.EmailConfirmed) throw new RuleException("unverified", 403);
        cached = new(id, user.DisplayName, user.Email!, (await users.GetRolesAsync(user)).ToArray(),
            await db.Scopes.Where(s => s.UserId == id && db.Departments.Any(d => d.Id == s.DepartmentId && d.Active))
                .Select(s => s.DepartmentId).ToArrayAsync());
        return cached;
    }

    public static void Require(bool permitted)
    {
        if (!permitted) throw new RuleException("forbidden", 403);
    }

    public async Task<bool> ItemReadable(Actor actor, FoundItem found)
    {
        return actor.Admin || found.UploaderId == actor.Id || actor.Custodian(found) ||
            await db.Handovers.AnyAsync(h => h.ItemId == found.Id && actor.Scopes.Contains(h.DepartmentId) &&
                actor.Roles.Contains("department") && (h.Status == "pending" || h.Status == "not-received")) ||
            await db.Claims.AnyAsync(c => c.ItemId == found.Id && c.RequesterId == actor.Id) ||
            (found.State == "available" && await db.SearchGrants.AnyAsync(g => g.UserId == actor.Id && g.ItemId == found.Id && g.ExpiresAt > DateTimeOffset.UtcNow));
    }
}

public sealed class Events(CampusDb db)
{
    public void Audit(Actor actor, string action, Guid entityId) =>
        db.Audit.Add(new() { ActorId = actor.Id, Action = action, EntityId = entityId });

    public void Notify(IEnumerable<Guid> recipients, string kind, string entityType, Guid entityId)
    {
        foreach (var recipient in recipients.Distinct())
            db.Notifications.Add(new() { RecipientId = recipient, Kind = kind, EntityType = entityType, EntityId = entityId });
    }

    public async Task<Guid[]> Custodians(FoundItem found)
    {
        if (found.CustodianId.HasValue) return [found.CustodianId.Value];
        var roleId = await db.Roles.Where(r => r.Name == "department").Select(r => r.Id).SingleAsync();
        return await db.Scopes.Where(s => s.DepartmentId == found.CustodianDepartmentId &&
                db.UserRoles.Any(r => r.UserId == s.UserId && r.RoleId == roleId))
            .Select(s => s.UserId).Distinct().ToArrayAsync();
    }
}
