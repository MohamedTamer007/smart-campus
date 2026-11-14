using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using SkiaSharp;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace SmartCampus.Tests;

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute() { if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB"))) Skip = "Requires isolated SMART_CAMPUS_TEST_DB"; }
}

public sealed class CampusFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string Password { get; } = "Local-test-" + Guid.NewGuid().ToString("N");
    public string MailDirectory { get; } = Path.Combine(Path.GetTempPath(), "smart-campus-tests", Guid.NewGuid().ToString("N"), "mail");
    public Guid Student { get; } = Guid.NewGuid();
    public Guid Other { get; } = Guid.NewGuid();
    public Guid Staff { get; } = Guid.NewGuid();
    public Guid StaffTwo { get; } = Guid.NewGuid();
    public Guid Outsider { get; } = Guid.NewGuid();
    public Guid Admin { get; } = Guid.NewGuid();
    public Guid Unverified { get; } = Guid.NewGuid();
    public Guid Department { get; } = Guid.NewGuid();
    public Guid SecondDepartment { get; } = Guid.NewGuid();
    public Guid Faculty { get; } = Guid.NewGuid();
    public Guid Building { get; } = Guid.NewGuid();
    public Guid Floor { get; } = Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var connection = Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB") ?? "";
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Database?.EndsWith("_test", StringComparison.Ordinal) != true || parsed.Host is not ("localhost" or "127.0.0.1"))
            throw new InvalidOperationException("Integration tests require a loopback database ending _test");
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["PublicOrigin"] = "http://localhost", ["Email:LocalSink"] = "true", ["Email:SinkDirectory"] = MailDirectory, ["RateLimits:AuthPerMinute"] = "1000", ["RateLimits:PerMinute"] = "10000",
            ["Storage:Local"] = "true", ["Storage:LocalDirectory"] = Path.Combine(Path.GetDirectoryName(MailDirectory)!, "media")
        }));
        builder.ConfigureServices(services => {
            services.RemoveAll<DbContextOptions<CampusDb>>();
            services.AddDbContext<CampusDb>(options => options.UseNpgsql(connection, pg => pg.MigrationsHistoryTable("__EFMigrationsHistory", "smart_campus")));
            services.RemoveAll<IHostedService>();
        });
    }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB"))) return;
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS smart_campus CASCADE");
        await db.Database.MigrateAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<CampusUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "user", "maintenance", "department", "admin" }) Assert.True((await roles.CreateAsync(new(role))).Succeeded);
        foreach (var id in new[] { Student, Other, Staff, StaffTwo, Outsider, Admin, Unverified })
        {
            var user = new CampusUser { Id = id, UserName = Email(id), Email = Email(id), DisplayName = "Test " + id.ToString("N")[..6], EmailConfirmed = id != Unverified };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            if (id == Staff || id == StaffTwo || id == Outsider) Assert.True((await users.AddToRolesAsync(user, ["maintenance", "department"])).Succeeded);
            if (id == Admin) Assert.True((await users.AddToRoleAsync(user, "admin")).Succeeded);
        }
        db.Departments.AddRange(new Department { Id = Department, Code = "test-maintenance", Ar = "اختبار", En = "TEST ONLY", Maintenance = true, CanReceive = true },
            new Department { Id = SecondDepartment, Code = "test-other", Ar = "اختبار آخر", En = "TEST ONLY OTHER", Maintenance = true, CanReceive = true });
        foreach (var id in new[] { Staff, StaffTwo, Outsider }) db.Scopes.Add(new() { UserId = id, DepartmentId = id == Outsider ? SecondDepartment : Department, GrantedBy = Admin });
        db.Locations.AddRange(new CampusLocation { Id = Faculty, Kind = "faculty", Ar = "اختبار", En = "TEST ONLY FACULTY", MaintenanceDepartmentId = Department },
            new CampusLocation { Id = Building, Kind = "building", ParentId = Faculty, Ar = "اختبار", En = "TEST ONLY BUILDING" },
            new CampusLocation { Id = Floor, Kind = "floor", ParentId = Building, Ar = "اختبار", En = "TEST ONLY FLOOR" });
        await db.SaveChangesAsync();
    }
    Task IAsyncLifetime.DisposeAsync() { Dispose(); return Task.CompletedTask; }

    public string Email(Guid id) => id.ToString("N") + "@o6u.edu.eg";
    public async Task<HttpClient> Login(Guid id)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await Csrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = Email(id), password = Password });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        await Csrf(client);
        return client;
    }
    public static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/v1/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
    public async Task<Guid> Report(HttpClient client)
    {
        using var form = new MultipartFormDataContent { { new StringContent("Test leak"), "description" },
            { new StringContent(Faculty.ToString()), "faculty" }, { new StringContent(Building.ToString()), "building" },
            { new StringContent(Floor.ToString()), "floor" }, { new StringContent("Test sink"), "details" } };
        return (await Response(await client.PostAsync("/api/v1/maintenance", form))).GetProperty("id").GetGuid();
    }
    public async Task<Guid> Item(HttpClient client)
    {
        using var bitmap = new SKBitmap(4, 4); bitmap.Erase(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var bytes = image.Encode(SKEncodedImageFormat.Png, 100);
        using var form = new MultipartFormDataContent { { new StringContent("Blue test bottle"), "description" },
            { new StringContent("TEST ONLY courtyard"), "place" }, { new StringContent("2026-01-01"), "date" },
            { new StringContent("With test uploader"), "storage" }, { new ByteArrayContent(bytes.ToArray()), "photo", "bottle.png" } };
        return (await Response(await client.PostAsync("/api/v1/found", form))).GetProperty("id").GetGuid();
    }
    public async Task<Guid> Claim(HttpClient client, Guid itemId, Guid userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        db.SearchGrants.Add(new() { ItemId = itemId, UserId = userId, ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15) });
        await db.SaveChangesAsync();
        return (await Response(await client.PostAsJsonAsync($"/api/v1/found/{itemId}/claims", new { message = "Test ownership evidence" }))).GetProperty("id").GetGuid();
    }
    public static async Task<JsonElement> Response(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }
}
