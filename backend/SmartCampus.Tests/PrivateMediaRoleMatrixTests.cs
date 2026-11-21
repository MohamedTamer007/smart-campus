using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

/// <summary>
/// G1/G5 evidence for server-gated private media in an isolated PostgreSQL-backed host.
/// These tests use only generated fictional accounts, synthetic image bytes, and local media.
/// They are NOT hosted identity/storage acceptance.
/// </summary>
public sealed class PrivateMediaRoleMatrixTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Found_image_visibility_follows_owner_admin_temporary_grant_and_department_handover()
    {
        using var owner = await campus.Login(campus.Student);
        using var unrelated = await campus.Login(campus.Other);
        using var receivingStaff = await campus.Login(campus.Staff);
        using var sameScopeStaff = await campus.Login(campus.StaffTwo);
        using var foreignDepartmentStaff = await campus.Login(campus.Outsider);
        using var administrator = await campus.Login(campus.Admin);
        using var guest = campus.CreateClient();

        var id = await campus.Item(owner);
        var detail = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/found/{id}");
        var url = detail.GetProperty("mediaUrl").GetString()!;
        Assert.StartsWith("/api/v1/media/", url);
        var itemVersion = detail.GetProperty("version").GetInt32();

        static async Task AssertStatus(HttpClient client, string route, HttpStatusCode expected)
        {
            using var response = await client.GetAsync(route);
            Assert.Equal(expected, response.StatusCode);
        }

        // Unrelated students and staff are forbidden even if they know the private URL.
        await AssertStatus(guest, url, HttpStatusCode.Unauthorized);
        await AssertStatus(unrelated, url, HttpStatusCode.Forbidden);
        await AssertStatus(receivingStaff, url, HttpStatusCode.Forbidden);
        await AssertStatus(sameScopeStaff, url, HttpStatusCode.Forbidden);
        await AssertStatus(foreignDepartmentStaff, url, HttpStatusCode.Forbidden);
        await AssertStatus(owner, url, HttpStatusCode.OK);
        await AssertStatus(administrator, url, HttpStatusCode.OK);

        // A search grant permits just the intended student, temporarily.
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            db.SearchGrants.Add(new() { ItemId = id, UserId = campus.Other,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(10) });
            await db.SaveChangesAsync();
        }
        await AssertStatus(unrelated, url, HttpStatusCode.OK);
        await AssertStatus(foreignDepartmentStaff, url, HttpStatusCode.Forbidden);
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            var grant = await db.SearchGrants.SingleAsync(g => g.ItemId == id && g.UserId == campus.Other);
            grant.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }
        await AssertStatus(unrelated, url, HttpStatusCode.Forbidden);

        // Starting a handover must grant the *receiving* department only.
        using var request = await owner.PostAsJsonAsync($"/api/v1/found/{id}/handovers",
            new { version = itemVersion, department = campus.Department, office = "TEST ONLY synthetic receiving desk" });
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        await AssertStatus(receivingStaff, url, HttpStatusCode.OK);
        await AssertStatus(sameScopeStaff, url, HttpStatusCode.OK);
        await AssertStatus(foreignDepartmentStaff, url, HttpStatusCode.Forbidden);
        await AssertStatus(unrelated, url, HttpStatusCode.Forbidden);
        await AssertStatus(owner, url, HttpStatusCode.OK);
    }
}
