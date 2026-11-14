using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed record FoundInput(string Description, string Place, DateOnly Date, string Storage, Guid? Destination, string? Office);
public sealed record HandoverInput(int Version, Guid Department, string Office);
public sealed record ClaimDecision(int Version, string? Phone, string? Place, string? Instructions, string? Reason);

public sealed class LostFoundService(CampusDb db, Access access, Events events, PrivateMedia media)
{
    private sealed record ListData(Dictionary<Guid, CustodyHandover> Handovers, Dictionary<Guid, Guid> MediaIds,
        Dictionary<Guid, string> DepartmentCodes, HashSet<Guid> GrantedItems, HashSet<Guid> ActiveClaimItems);

    public async Task<object> Register(FoundInput input, IFormFile photo)
    {
        var actor = await access.Current();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")).DateTime);
        if (input.Date > today || input.Date == default) throw new RuleException("invalid_date");
        var found = new FoundItem { UploaderId = actor.Id, CustodianId = actor.Id, Description = Rules.Required(input.Description),
            Place = Rules.Required(input.Place, 1000), Date = input.Date, Storage = Rules.Required(input.Storage, 1000) };
        if (input.Destination.HasValue) await ReceivingDepartment(input.Destination.Value);
        var image = await media.Upload(photo, new() { ItemId = found.Id, Bucket = "lost-found-images" });
        try
        {
            db.Items.Add(found);
            db.Media.Add(image);
            if (input.Destination.HasValue)
                db.Handovers.Add(new() { ItemId = found.Id, FromUserId = actor.Id, DepartmentId = input.Destination.Value,
                    Office = Rules.Required(input.Office, 1000) });
            db.Jobs.Add(new() { Kind = "index", EntityId = found.Id, InputVersion = 1, Key = $"index:{found.Id}:1" });
            events.Audit(actor, "FoundRegistered", found.Id);
            if (input.Destination.HasValue) await NotifyReceiving(input.Destination.Value, found.Id);
            await db.SaveChangesAsync();
        }
        catch { await media.Delete(image); throw; }
        return await Detail(found.Id);
    }

    public async Task<object[]> List(int page, int pageSize)
    {
        var actor = await access.Current();
        var (offset, size) = Rules.PageWindow(page, pageSize);
        var now = DateTimeOffset.UtcNow;
        var items = await db.Items.AsNoTracking().Where(f => actor.Admin || f.UploaderId == actor.Id || f.CustodianId == actor.Id ||
            (actor.Roles.Contains("department") && actor.Scopes.Contains(f.CustodianDepartmentId ?? Guid.Empty)) ||
            db.Claims.Any(c => c.ItemId == f.Id && c.RequesterId == actor.Id) ||
            (f.State == "available" && db.SearchGrants.Any(g => g.ItemId == f.Id && g.UserId == actor.Id && g.ExpiresAt > now)) ||
            (actor.Roles.Contains("department") && db.Handovers.Any(h => h.ItemId == f.Id && actor.Scopes.Contains(h.DepartmentId) &&
                (h.Status == "pending" || h.Status == "not-received"))))
            .OrderByDescending(f => f.CreatedAt).ThenBy(f => f.Id).Skip(offset).Take(size).ToArrayAsync();
        if (items.Length == 0) return [];
        var related = await Related(items, actor, now);
        return items.Select(item => Response(item, actor, related)).ToArray();
    }

    public async Task<object> Detail(Guid id)
    {
        var actor = await access.Current();
        var found = await db.Items.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id) ?? throw new RuleException("not_found", 404);
        Access.Require(await access.ItemReadable(actor, found));
        return Response(found, actor, await Related([found], actor, DateTimeOffset.UtcNow));
    }

    private async Task<ListData> Related(FoundItem[] items, Actor actor, DateTimeOffset now)
    {
        var ids = items.Select(item => item.Id).ToArray();
        var handovers = await db.Handovers.AsNoTracking().Where(h => ids.Contains(h.ItemId))
            .GroupBy(h => h.ItemId).Select(group => group.OrderByDescending(h => h.CreatedAt).First()).ToArrayAsync();
        var handoverMap = handovers.ToDictionary(h => h.ItemId);
        var media = await db.Media.AsNoTracking().Where(m => m.ItemId.HasValue && ids.Contains(m.ItemId.Value))
            .OrderBy(m => m.CreatedAt).Select(m => new { ItemId = m.ItemId!.Value, m.Id }).ToArrayAsync();
        var mediaMap = media.GroupBy(m => m.ItemId).ToDictionary(group => group.Key, group => group.First().Id);
        var departmentIds = items.Where(item => item.CustodianDepartmentId.HasValue).Select(item => item.CustodianDepartmentId!.Value)
            .Concat(handovers.Select(h => h.DepartmentId)).Distinct().ToArray();
        var departments = await db.Departments.AsNoTracking().Where(department => departmentIds.Contains(department.Id))
            .Select(department => new { department.Id, department.Code }).ToDictionaryAsync(department => department.Id, department => department.Code);
        var granted = await db.SearchGrants.AsNoTracking().Where(grant => ids.Contains(grant.ItemId) && grant.UserId == actor.Id && grant.ExpiresAt > now)
            .Select(grant => grant.ItemId).Distinct().ToHashSetAsync();
        var activeClaims = await db.Claims.AsNoTracking().Where(claim => ids.Contains(claim.ItemId) && claim.RequesterId == actor.Id &&
                (claim.Status == "pending" || claim.Status == "approved"))
            .Select(claim => claim.ItemId).Distinct().ToHashSetAsync();
        return new(handoverMap, mediaMap, departments, granted, activeClaims);
    }

    private static object Response(FoundItem found, Actor actor, ListData related)
    {
        related.Handovers.TryGetValue(found.Id, out var handover);
        var department = found.CustodianDepartmentId.HasValue && related.DepartmentCodes.TryGetValue(found.CustodianDepartmentId.Value, out var code)
            ? code : null;
        var destination = handover is not null && related.DepartmentCodes.TryGetValue(handover.DepartmentId, out var destinationCode)
            ? destinationCode : null;
        var canClaim = found.State == "available" && !actor.Custodian(found) && related.GrantedItems.Contains(found.Id) &&
            !related.ActiveClaimItems.Contains(found.Id);
        var mediaUrl = related.MediaIds.TryGetValue(found.Id, out var mediaId) ? $"/api/v1/media/{mediaId}" : null;
        var handoverVisible = handover is not null && (actor.Admin || actor.Custodian(found) || actor.Department(handover.DepartmentId));
        return new { found.Id, uploaderId = found.UploaderId, custodianId = found.CustodianId, custodianDepartment = department,
            found.Description, found.Place, found.Date, storage = actor.Admin || actor.Custodian(found) ? found.Storage : null,
            found.State, found.Version, found.CreatedAt,
            mediaUrl, canClaim,
            canHandover = actor.Custodian(found) && found.State == "available" && (handover is null || handover.Status is "confirmed" or "cancelled"),
            canReceive = handover is not null && actor.Department(handover.DepartmentId) && handover.Status is "pending" or "not-received" && found.State == "available",
            canCancelHandover = actor.Custodian(found) && handover is not null && handover.Status is "pending" or "not-received",
            handover = handoverVisible ? new { handover!.Id, handover.Version, department = destination, handover.Office, handover.Status } : null };
    }

    public async Task<object> RequestHandover(Guid id, HandoverInput input)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var found = await Locked(id);
        Access.Require(actor.Custodian(found));
        Rules.Version(found.Version, input.Version);
        Available(found);
        await ReceivingDepartment(input.Department);
        if (found.CustodianDepartmentId == input.Department) throw new RuleException("invalid_destination");
        if (await db.Handovers.AnyAsync(h => h.ItemId == id && (h.Status == "pending" || h.Status == "not-received"))) throw new RuleException("conflict", 409);
        db.Handovers.Add(new() { ItemId = id, FromUserId = found.CustodianId, FromDepartmentId = found.CustodianDepartmentId,
            DepartmentId = input.Department, Office = Rules.Required(input.Office, 1000) });
        found.Version++;
        events.Audit(actor, "HandoverRequested", id);
        await NotifyReceiving(input.Department, id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await Detail(id);
    }

    public async Task<object> HandoverAction(Guid id, int version, string action)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var found = await Locked(id);
        var handover = await db.Handovers.Where(h => h.ItemId == id).OrderByDescending(h => h.CreatedAt).FirstOrDefaultAsync()
            ?? throw new RuleException("not_found", 404);
        Access.Require(action == "cancelled" ? actor.Custodian(found) : actor.Department(handover.DepartmentId));
        if (handover.Status == action) return await Detail(id);
        Rules.Version(found.Version, version);
        if (handover.Status is not ("pending" or "not-received")) throw new RuleException("invalid_transition", 409);
        if (handover.FromUserId != found.CustodianId || handover.FromDepartmentId != found.CustodianDepartmentId)
            throw new RuleException("conflict", 409);
        if (action == "confirmed")
        {
            Available(found);
            found.CustodianId = null;
            found.CustodianDepartmentId = handover.DepartmentId;
            found.Storage = handover.Office;
            handover.ReceivedBy = actor.Id;
            handover.ReceivedAt = DateTimeOffset.UtcNow;
        }
        else if (action == "not-received") { handover.NotReceivedBy = actor.Id; handover.NotReceivedAt = DateTimeOffset.UtcNow; }
        else if (action == "cancelled") handover.CancelledAt = DateTimeOffset.UtcNow;
        else throw new RuleException("invalid_input");
        handover.Status = action;
        handover.Version++;
        found.Version++;
        events.Audit(actor, "Handover:" + action, id);
        events.Notify([found.UploaderId, ..await events.Custodians(found)], action, "found", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await Detail(id);
    }

    public async Task<object> Claim(Guid id, string? message)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var found = await Locked(id);
        Access.Require(!actor.Custodian(found) && await db.SearchGrants.AnyAsync(g => g.ItemId == id && g.UserId == actor.Id && g.ExpiresAt > DateTimeOffset.UtcNow));
        Available(found);
        if (message?.Length > 4000) throw new RuleException("invalid_input");
        var claim = new RecoveryRequest { ItemId = id, RequesterId = actor.Id, Message = message?.Trim() ?? "" };
        db.Claims.Add(claim);
        events.Audit(actor, "RecoveryRequested", claim.Id);
        events.Notify(await events.Custodians(found), "requested", "claim", claim.Id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await ClaimDto(claim, actor, found);
    }

    public async Task<object[]> Claims(int page, int pageSize)
    {
        var actor = await access.Current();
        var (offset, size) = Rules.PageWindow(page, pageSize);
        var claims = await (from claim in db.Claims.AsNoTracking()
            join item in db.Items on claim.ItemId equals item.Id
            join requester in db.Users on claim.RequesterId equals requester.Id
            where claim.RequesterId == actor.Id || actor.Admin || item.CustodianId == actor.Id ||
                (actor.Roles.Contains("department") && actor.Scopes.Contains(item.CustodianDepartmentId ?? Guid.Empty))
            orderby claim.CreatedAt descending, claim.Id
            select new { Claim = claim, Item = item, RequesterName = requester.DisplayName })
            .Skip(offset).Take(size).ToArrayAsync();
        return claims.Select(row => ClaimResponse(row.Claim, actor, row.Item, row.RequesterName)).ToArray();
    }

    public async Task<object> ClaimDetail(Guid id)
    {
        var actor = await access.Current();
        var claim = await db.Claims.FindAsync(id) ?? throw new RuleException("not_found", 404);
        var found = (await db.Items.FindAsync(claim.ItemId))!;
        Access.Require(actor.Admin || actor.Id == claim.RequesterId || actor.Custodian(found));
        return new { claim = await ClaimDto(claim, actor, found), item = await Detail(found.Id) };
    }

    public async Task<object> Decide(Guid id, ClaimDecision input, string action)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var itemId = await db.Claims.AsNoTracking().Where(c => c.Id == id).Select(c => (Guid?)c.ItemId).SingleOrDefaultAsync()
            ?? throw new RuleException("not_found", 404);
        var found = await Locked(itemId);
        var claim = (await db.Claims.FindAsync(id))!;
        Access.Require(actor.Custodian(found));
        Rules.Version(claim.Version, input.Version);
        if (action == "approved") Access.Require(actor.Id != claim.RequesterId);
        if (action == "cancelled") CancelApproval(claim, found, input.Reason);
        else
        {
            if (claim.Status != "pending") throw new RuleException("invalid_transition", 409);
            if (action == "approved") Approve(claim, found, input);
            else if (action == "rejected") { claim.Status = "rejected"; claim.Reason = Rules.Required(input.Reason); }
            else throw new RuleException("invalid_input");
        }
        claim.ReviewedBy = actor.Id;
        claim.ReviewedAt = DateTimeOffset.UtcNow;
        claim.Version++;
        found.Version++;
        events.Audit(actor, "Recovery:" + action, id);
        events.Notify([claim.RequesterId], action, "claim", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await ClaimDto(claim, actor, found);
    }

    public async Task<object> Confirm(Guid id, int version)
    {
        var actor = await access.Current();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var itemId = await db.Claims.AsNoTracking().Where(c => c.Id == id).Select(c => (Guid?)c.ItemId).SingleOrDefaultAsync()
            ?? throw new RuleException("not_found", 404);
        var found = await Locked(itemId);
        var claim = (await db.Claims.FindAsync(id))!;
        var requester = actor.Id == claim.RequesterId;
        Access.Require(requester || actor.Custodian(found));
        if ((requester && claim.ReceivedAt.HasValue) || (!requester && claim.DeliveredAt.HasValue)) return await ClaimDto(claim, actor, found);
        Rules.Version(claim.Version, version);
        if (claim.Status != "approved" || !claim.ReservationActive || found.State != "reserved") throw new RuleException("invalid_transition", 409);
        if (requester) claim.ReceivedAt = DateTimeOffset.UtcNow;
        else { claim.DeliveredAt = DateTimeOffset.UtcNow; claim.DeliveredBy = actor.Id; }
        if (claim.ReceivedAt.HasValue && claim.DeliveredAt.HasValue)
        {
            claim.Status = "closed";
            claim.ClosedAt = DateTimeOffset.UtcNow;
            claim.ClosureReason = "Recovered";
            claim.ReservationActive = false;
            found.State = "delivered";
            var competing = await db.Claims.Where(c => c.ItemId == found.Id && c.Id != id && c.Status == "pending").ToArrayAsync();
            foreach (var other in competing)
            {
                other.Status = "closed"; other.ClosureReason = "ItemRecovered"; other.ClosedAt = claim.ClosedAt; other.Version++;
                events.Notify([other.RequesterId], "closed", "claim", other.Id);
            }
        }
        claim.Version++;
        found.Version++;
        events.Audit(actor, requester ? "ReceiptConfirmed" : "DeliveryConfirmed", id);
        events.Notify([claim.RequesterId, ..await events.Custodians(found)], claim.Status, "claim", id);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await ClaimDto(claim, actor, found);
    }

    private static void Approve(RecoveryRequest claim, FoundItem found, ClaimDecision input)
    {
        Available(found);
        if (found.CustodianId.HasValue) claim.Phone = Rules.Required(input.Phone, 30);
        else { claim.Place = Rules.Required(input.Place, 1000); claim.Instructions = Rules.Required(input.Instructions); }
        claim.Status = "approved";
        claim.ReservationActive = true;
        found.State = "reserved";
    }

    private static void CancelApproval(RecoveryRequest claim, FoundItem found, string? reason)
    {
        if (claim.Status != "approved" || !claim.ReservationActive || claim.DeliveredAt.HasValue || claim.ReceivedAt.HasValue)
            throw new RuleException("invalid_transition", 409);
        claim.Status = "cancelled";
        claim.ReservationActive = false;
        claim.Reason = Rules.Required(reason);
        found.State = "available";
    }

    private async Task<object> ClaimDto(RecoveryRequest claim, Actor actor, FoundItem found)
    {
        var claimant = await db.Users.Where(u => u.Id == claim.RequesterId).Select(u => u.DisplayName).SingleAsync();
        return ClaimResponse(claim, actor, found, claimant);
    }

    private static object ClaimResponse(RecoveryRequest claim, Actor actor, FoundItem found, string claimant)
    {
        var privateAccess = (claim.RequesterId == actor.Id && claim.Status is "approved" or "closed") || actor.Custodian(found) || actor.Admin;
        return new { claim.Id, claim.ItemId, claimantId = claim.RequesterId, claimantName = claimant, claimantRole = "user",
            claim.CreatedAt, claim.Message, claim.Status, claim.Version, phone = privateAccess ? claim.Phone : null,
            place = privateAccess ? claim.Place : null, instructions = privateAccess ? claim.Instructions : null,
            claim.Reason, claim.ClosureReason, claimantConfirmed = claim.ReceivedAt.HasValue, custodianConfirmed = claim.DeliveredAt.HasValue,
            claimantAt = claim.ReceivedAt, custodianAt = claim.DeliveredAt,
            canApprove = actor.Custodian(found) && found.State == "available" && claim.Status == "pending",
            canReject = actor.Custodian(found) && claim.Status == "pending",
            canCancel = actor.Custodian(found) && claim.Status == "approved" && claim.DeliveredAt is null && claim.ReceivedAt is null };
    }

    private async Task ReceivingDepartment(Guid id)
    {
        if (!await db.Departments.AnyAsync(d => d.Id == id && d.Active && d.CanReceive) ||
            !await db.Scopes.AnyAsync(s => s.DepartmentId == id && db.Users.Any(u => u.Id == s.UserId && u.Active && u.EmailConfirmed) &&
                db.UserRoles.Any(r => r.UserId == s.UserId && db.Roles.Any(role => role.Id == r.RoleId && role.Name == "department"))))
            throw new RuleException("department_unavailable", 503);
    }
    private async Task<FoundItem> Locked(Guid id) => await db.Items.FromSqlInterpolated($"SELECT * FROM smart_campus.\"Items\" WHERE \"Id\" = {id} FOR UPDATE").SingleOrDefaultAsync()
        ?? throw new RuleException("not_found", 404);
    private static void Available(FoundItem found)
    {
        if (found.State != "available") throw new RuleException("item_unavailable", 409);
    }
    private async Task NotifyReceiving(Guid departmentId, Guid itemId)
    {
        var recipients = await db.Scopes.Where(s => s.DepartmentId == departmentId &&
            db.Users.Any(user => user.Id == s.UserId && user.Active && user.EmailConfirmed) && db.UserRoles.Any(r => r.UserId == s.UserId &&
            db.Roles.Any(role => role.Id == r.RoleId && role.Name == "department"))).Select(s => s.UserId).ToArrayAsync();
        events.Notify(recipients, "handover_requested", "found", itemId);
    }
}
