using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public static class DemoSeed
{
    public static async Task Create(IServiceProvider services, IConfiguration config)
    {
        var db = services.GetRequiredService<CampusDb>();
        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (connection.Host is not ("localhost" or "127.0.0.1") || connection.Database?.EndsWith("_dev", StringComparison.Ordinal) != true)
            throw new InvalidOperationException("Demo seed requires loopback database ending _dev");
        var file = Path.GetFullPath(config["Demo:AccountsFile"] ?? "local-data/demo-accounts.json");
        if (File.Exists(file)) throw new InvalidOperationException("Demo account receipt exists; refusing duplicate seed");
        var users = services.GetRequiredService<UserManager<CampusUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var name in new[] { "user", "maintenance", "department", "admin" })
            if (!await roles.RoleExistsAsync(name)) Ensure(await roles.CreateAsync(new(name)));
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var accounts = new Dictionary<string, CampusUser>();
        foreach (var name in new[] { "student", "requester", "staff", "admin" })
        {
            var user = new CampusUser { Id = Guid.NewGuid(), UserName = $"demo-{name}-{Guid.NewGuid():N}@o6u.edu.eg", DisplayName = "DEMO ONLY " + name, EmailConfirmed = true };
            user.Email = user.UserName;
            Ensure(await users.CreateAsync(user, password));
            accounts[name] = user;
        }
        Ensure(await users.AddToRolesAsync(accounts["staff"], ["maintenance", "department"]));
        Ensure(await users.AddToRoleAsync(accounts["admin"], "admin"));
        var department = new Department { Code = "demo-maintenance-" + Guid.NewGuid().ToString("N")[..6], Ar = "جهة تجريبية فقط", En = "DEMO ONLY department", Maintenance = true, CanReceive = true };
        db.Departments.Add(department);
        db.Scopes.Add(new() { UserId = accounts["staff"].Id, DepartmentId = department.Id, GrantedBy = accounts["admin"].Id });
        var faculty = new CampusLocation { Kind = "faculty", Ar = "مرفق تجريبي فقط", En = "DEMO ONLY faculty", MaintenanceDepartmentId = department.Id };
        var building = new CampusLocation { Kind = "building", ParentId = faculty.Id, Ar = "مبنى تجريبي فقط", En = "DEMO ONLY building" };
        var floor = new CampusLocation { Kind = "floor", ParentId = building.Id, Ar = "دور تجريبي فقط", En = "DEMO ONLY floor" };
        db.Locations.AddRange(faculty, building, floor);
        await db.SaveChangesAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { password, accounts = accounts.ToDictionary(p => p.Key, p => new { id = p.Value.Id, email = p.Value.Email }), departmentId = department.Id, facultyId = faculty.Id, buildingId = building.Id, floorId = floor.Id }));
    }
    private static void Ensure(IdentityResult outcome)
    {
        if (!outcome.Succeeded) throw new InvalidOperationException(string.Join(',', outcome.Errors.Select(e => e.Code)));
    }
}
