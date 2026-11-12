using System.Security.Cryptography;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed record DemoCredential(string Role, string Email, string? Password, bool Created);
public sealed record DemoPreparationStatus(bool Enabled, bool AlreadyPrepared);
internal sealed record DemoAccount(string Key, string Email, string Name, string Role);
internal sealed record DemoFoundItemSpec(Guid? DepartmentId, string Description, string Place, string Storage);
internal sealed record DemoDirectory(Department Maintenance, Department Receiving, CampusLocation Faculty,
    CampusLocation Building, CampusLocation Floor);
internal sealed record DemoItems(FoundItem Notebook, FoundItem Umbrella);
internal sealed record DemoPreparation(List<DemoCredential> Credentials, DemoItems Items);

/// <summary>Creates isolated, conspicuously fictional accounts and records for an approved walkthrough.</summary>
public sealed class DemoPreparationService(
    CampusDb db,
    UserManager<CampusUser> users,
    RoleManager<IdentityRole<Guid>> roles,
    PrivateMedia media)
{
    public const string DemoMarkerClaimType = "smartcampus:demo-only";
    private static readonly DemoAccount[] AccountSpecs =
    [
        new("student", "demo.student@o6u.edu.eg", "DEMO ONLY · Student", "user"),
        new("requester", "demo.requester@o6u.edu.eg", "DEMO ONLY · Recovery requester", "user"),
        new("maintenance", "demo.maintenance@o6u.edu.eg", "DEMO ONLY · Maintenance staff", "maintenance"),
        new("department", "demo.department@o6u.edu.eg", "DEMO ONLY · Receiving department", "department"),
        new("admin", "demo.admin@o6u.edu.eg", "DEMO ONLY · Administrator", "admin")
    ];

    public async Task<bool> IsPrepared(CancellationToken cancellationToken) => await AlreadyPrepared(cancellationToken);

    public async Task<object> Prepare(Actor actor, CancellationToken cancellationToken)
    {
        if (await AlreadyPrepared(cancellationToken))
            return await PreparedResult(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var createdMedia = new List<MediaObject>();
        try
        {
            var preparation = await CreateDemoRecords(actor, createdMedia, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return BuildPreparationResponse(preparation, await ExistingGrantExpiry(cancellationToken));
        }
        catch
        {
            await DeleteUploadedMedia(createdMedia);
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<DemoPreparation> CreateDemoRecords(Actor actor, List<MediaObject> createdMedia, CancellationToken cancellationToken)
    {
        await EnsureRoles();
        var credentials = new List<DemoCredential>();
        var accounts = await CreateAccounts(credentials, cancellationToken);
        var directory = await CreateDemoDirectory(accounts, actor.Id);
        await CreateMaintenanceReport(accounts["student"], directory);
        var items = await CreateDemoItems(accounts["student"], directory.Receiving.Id, createdMedia, cancellationToken);
        await GrantRecoveryItems(accounts["requester"].Id, items);
        db.Audit.Add(new AuditEvent { ActorId = actor.Id, Action = "DemoEnvironmentPrepared", EntityId = accounts["admin"].Id });
        return new DemoPreparation(credentials, items);
    }

    private async Task EnsureRoles()
    {
        foreach (var roleName in new[] { "user", "maintenance", "department", "admin" })
            if (!await roles.RoleExistsAsync(roleName))
                Ensure(await roles.CreateAsync(new IdentityRole<Guid>(roleName)));
    }

    private async Task<Dictionary<string, CampusUser>> CreateAccounts(List<DemoCredential> credentials, CancellationToken cancellationToken)
    {
        var accounts = new Dictionary<string, CampusUser>(StringComparer.Ordinal);
        foreach (var accountSpec in AccountSpecs)
        {
            var account = await CreateAccount(accountSpec, cancellationToken);
            accounts.Add(accountSpec.Key, account.User);
            credentials.Add(new(accountSpec.Key, accountSpec.Email, account.Password, account.Created));
        }
        return accounts;
    }

    private async Task<(CampusUser User, string? Password, bool Created)> CreateAccount(DemoAccount spec, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(spec.Email);
        var isNew = user is null;
        var password = isNew ? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) : null;
        user ??= NewDemoUser(spec);
        if (isNew) await AddDemoIdentity(user, password!);
        else await RestoreDemoIdentity(user, cancellationToken);
        await AddMissingRole(user, spec.Role);
        return (user, isNew ? password : null, isNew);
    }

    private static CampusUser NewDemoUser(DemoAccount spec) => new()
    {
        Id = Guid.NewGuid(), UserName = spec.Email, Email = spec.Email,
        DisplayName = spec.Name, EmailConfirmed = true
    };

    private async Task AddDemoIdentity(CampusUser user, string password)
    {
        Ensure(await users.CreateAsync(user, password));
        Ensure(await users.AddClaimAsync(user, new Claim(DemoMarkerClaimType, "true")));
    }

    private async Task RestoreDemoIdentity(CampusUser user, CancellationToken cancellationToken)
    {
        if (!await IsDemoUser(user.Id, cancellationToken)) throw new RuleException("demo_account_conflict", 409);
        user.EmailConfirmed = true;
        user.Active = true;
    }

    private async Task AddMissingRole(CampusUser user, string roleName)
    {
        var currentRoles = await users.GetRolesAsync(user);
        if (!currentRoles.Contains(roleName, StringComparer.Ordinal)) Ensure(await users.AddToRoleAsync(user, roleName));
    }

    private async Task<DemoDirectory> CreateDemoDirectory(Dictionary<string, CampusUser> accounts, Guid actorId)
    {
        var maintenance = await Department("DEMO ONLY MAINTENANCE", "جهة صيانة تجريبية فقط", true, false);
        var receiving = await Department("DEMO ONLY RECEIVING", "جهة استلام تجريبية فقط", false, true);
        await Scope(accounts["maintenance"].Id, maintenance.Id, actorId);
        await Scope(accounts["department"].Id, receiving.Id, actorId);
        return await CreateDemoLocations(maintenance, receiving);
    }

    private async Task<DemoDirectory> CreateDemoLocations(Department maintenance, Department receiving)
    {
        var faculty = await Location(null, "faculty", "DEMO ONLY · Fictional faculty", "كلية خيالية · بيانات تجريبية", maintenance.Id);
        var building = await Location(faculty.Id, "building", "DEMO ONLY · Fictional building", "مبنى خيالي · بيانات تجريبية", null);
        var floor = await Location(building.Id, "floor", "DEMO ONLY · Fictional floor", "دور خيالي · بيانات تجريبية", null);
        building.MapX ??= 36;
        building.MapY ??= 48;
        return new DemoDirectory(maintenance, receiving, faculty, building, floor);
    }

    private async Task CreateMaintenanceReport(CampusUser student, DemoDirectory directory)
    {
        const string description = "DEMO ONLY · Fictional water leak";
        if (await db.Reports.AnyAsync(x => x.ReporterId == student.Id && x.Description == description)) return;
        var report = new MaintenanceReport { ReporterId = student.Id, FacultyId = directory.Faculty.Id,
            BuildingId = directory.Building.Id, FloorId = directory.Floor.Id, DepartmentId = directory.Maintenance.Id,
            Description = description, Details = "DEMO ONLY · No real facility or person is represented." };
        db.Reports.Add(report);
        db.History.Add(new MaintenanceHistory { ReportId = report.Id, ActorId = student.Id,
            Status = "Submitted", Note = "DEMO ONLY · Synthetic walkthrough record." });
    }

    private async Task<DemoItems> CreateDemoItems(CampusUser student, Guid receivingId, List<MediaObject> createdMedia,
        CancellationToken cancellationToken)
    {
        var notebook = await FoundItem(new(null, "DEMO ONLY · Fictional blue notebook", "DEMO ONLY · Fictional courtyard",
            "DEMO ONLY · Demo office cabinet"), student, createdMedia, cancellationToken);
        var umbrella = await FoundItem(new(receivingId, "DEMO ONLY · Fictional red umbrella", "DEMO ONLY · Fictional library",
            "DEMO ONLY · Demo receiving office"), student, createdMedia, cancellationToken);
        return new DemoItems(notebook, umbrella);
    }

    private async Task GrantRecoveryItems(Guid requesterId, DemoItems items)
    {
        await GrantRecoveryItem(requesterId, items.Notebook.Id);
        await GrantRecoveryItem(requesterId, items.Umbrella.Id);
    }

    private async Task GrantRecoveryItem(Guid requesterId, Guid itemId)
    {
        if (await db.SearchGrants.AnyAsync(grant => grant.UserId == requesterId && grant.ItemId == itemId &&
                grant.ExpiresAt > DateTimeOffset.UtcNow)) return;
        db.SearchGrants.Add(new SearchGrant { UserId = requesterId, ItemId = itemId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });
    }

    private static object BuildPreparationResponse(DemoPreparation preparation, DateTimeOffset? grantExpiry) => new
    {
        prepared = true,
        demoOnly = true,
        recoveryGrantExpiresAt = grantExpiry,
        credentials = preparation.Credentials,
        records = new { maintenanceReport = "DEMO ONLY · Fictional water leak",
            recoveryItem = preparation.Items.Notebook.Description,
            departmentCustodyItem = preparation.Items.Umbrella.Description }
    };

    private async Task DeleteUploadedMedia(List<MediaObject> createdMedia)
    {
        foreach (var uploadedObject in createdMedia)
            try { await media.Delete(uploadedObject); } catch { /* rollback must still run after storage cleanup fails */ }
    }

    private async Task<object> PreparedResult(CancellationToken cancellationToken) => new
    {
        prepared = true,
        alreadyPrepared = true,
        recoveryGrantExpiresAt = await ExistingGrantExpiry(cancellationToken),
        credentials = Array.Empty<DemoCredential>(),
        records = new { maintenanceReport = "DEMO ONLY · Fictional water leak",
            recoveryItem = "DEMO ONLY · Fictional blue notebook",
            departmentCustodyItem = "DEMO ONLY · Fictional red umbrella" }
    };

    private async Task<bool> AlreadyPrepared(CancellationToken cancellationToken)
    {
        var accounts = await db.Users.Where(x => AccountSpecs.Select(s => s.Email).Contains(x.Email!)).ToArrayAsync(cancellationToken);
        if (accounts.Length != AccountSpecs.Length ||
            await db.UserClaims.CountAsync(c => accounts.Select(a => a.Id).Contains(c.UserId) &&
                c.ClaimType == DemoMarkerClaimType && c.ClaimValue == "true", cancellationToken) != AccountSpecs.Length)
            return false;
        var studentId = accounts.Single(x => x.Email == "demo.student@o6u.edu.eg").Id;
        var requesterId = accounts.Single(x => x.Email == "demo.requester@o6u.edu.eg").Id;
        var expectedItems = new[] { "DEMO ONLY · Fictional blue notebook", "DEMO ONLY · Fictional red umbrella" };
        return await db.Departments.AnyAsync(x => x.Code == "DEMO ONLY MAINTENANCE", cancellationToken) &&
            await db.Departments.AnyAsync(x => x.Code == "DEMO ONLY RECEIVING", cancellationToken) &&
            await db.Reports.AnyAsync(x => x.ReporterId == studentId && x.Description == "DEMO ONLY · Fictional water leak", cancellationToken) &&
            await db.Items.CountAsync(x => x.UploaderId == studentId && expectedItems.Contains(x.Description), cancellationToken) == expectedItems.Length &&
            await db.SearchGrants.AnyAsync(g => g.UserId == requesterId &&
                db.Items.Any(i => i.Id == g.ItemId && i.Description == expectedItems[0]), cancellationToken);
    }

    private Task<DateTimeOffset?> ExistingGrantExpiry(CancellationToken cancellationToken) => db.SearchGrants
        .Where(x => db.Users.Any(u => u.Id == x.UserId && u.Email == "demo.requester@o6u.edu.eg") &&
            db.Items.Any(i => i.Id == x.ItemId && i.Description == "DEMO ONLY · Fictional blue notebook"))
        .Select(x => (DateTimeOffset?)x.ExpiresAt).MaxAsync(cancellationToken);

    private async Task<Department> Department(string code, string arabic, bool maintenance, bool receiving)
    {
        var entity = await db.Departments.SingleOrDefaultAsync(x => x.Code == code);
        if (entity is not null)
        {
            if (!entity.En.StartsWith("DEMO ONLY ", StringComparison.Ordinal)) throw new RuleException("demo_directory_conflict", 409);
            entity.Active = true;
            return entity;
        }
        entity = new Department { Code = code, Ar = arabic, En = code, Maintenance = maintenance, CanReceive = receiving };
        db.Departments.Add(entity);
        return entity;
    }

    private async Task<CampusLocation> Location(Guid? parent, string kind, string english, string arabic, Guid? maintenanceDepartment)
    {
        var entity = await db.Locations.SingleOrDefaultAsync(x => x.Kind == kind && x.En == english);
        if (entity is not null)
        {
            if (!entity.En.StartsWith("DEMO ONLY ", StringComparison.Ordinal)) throw new RuleException("demo_directory_conflict", 409);
            return entity;
        }
        entity = new CampusLocation { ParentId = parent, Kind = kind, Ar = arabic, En = english,
            MaintenanceDepartmentId = maintenanceDepartment };
        db.Locations.Add(entity);
        return entity;
    }

    private async Task Scope(Guid userId, Guid departmentId, Guid grantedBy)
    {
        if (!await db.Scopes.AnyAsync(x => x.UserId == userId && x.DepartmentId == departmentId))
            db.Scopes.Add(new DepartmentScope { UserId = userId, DepartmentId = departmentId, GrantedBy = grantedBy });
    }

    private Task<bool> IsDemoUser(Guid userId, CancellationToken cancellationToken) => db.UserClaims.AnyAsync(
        c => c.UserId == userId && c.ClaimType == DemoMarkerClaimType && c.ClaimValue == "true", cancellationToken);

    private async Task<FoundItem> FoundItem(DemoFoundItemSpec spec, CampusUser uploader, List<MediaObject> createdMedia,
        CancellationToken cancellationToken)
    {
        var item = await db.Items.SingleOrDefaultAsync(x => x.UploaderId == uploader.Id && x.Description == spec.Description, cancellationToken);
        if (item is not null)
        {
            if (!spec.Description.StartsWith("DEMO ONLY ", StringComparison.Ordinal)) throw new RuleException("demo_item_conflict", 409);
            return item;
        }

        var image = await SyntheticPhoto("lost-found-images", cancellationToken);
        createdMedia.Add(image);
        item = new FoundItem { UploaderId = uploader.Id, CustodianId = spec.DepartmentId.HasValue ? null : uploader.Id,
            CustodianDepartmentId = spec.DepartmentId, Description = spec.Description, Place = spec.Place,
            Date = DateOnly.FromDateTime(DateTime.UtcNow), Storage = spec.Storage };
        image.ItemId = item.Id;
        db.Items.Add(item);
        db.Media.Add(image);
        return item;
    }

    private async Task<MediaObject> SyntheticPhoto(string bucket, CancellationToken cancellationToken)
    {
        using var bitmap = new SKBitmap(320, 200);
        bitmap.Erase(SKColors.LightSteelBlue);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = SKColors.Navy, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(95, 40, 225, 160), 12, 12, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        await using var stream = new MemoryStream(encoded.ToArray());
        var formFile = new FormFile(stream, 0, stream.Length, "photo", "demo-item.png")
        { Headers = new HeaderDictionary(), ContentType = "image/png" };
        return await media.Upload(formFile, new MediaObject { Bucket = bucket });
    }

    private static void Ensure(IdentityResult outcome)
    {
        if (!outcome.Succeeded) throw new RuleException("demo_preparation_failed", 409);
    }
}
