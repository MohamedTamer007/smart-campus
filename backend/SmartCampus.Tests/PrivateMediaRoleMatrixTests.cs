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
    public async Task Rejected_claim_retains_history_without_permanent_private_image_access()
    {
        using var owner = await campus.Login(campus.Student);
        using var requester = await campus.Login(campus.Other);
        using var admin = await campus.Login(campus.Admin);
        var itemId = await campus.Item(owner);
        var ownerView = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/found/{itemId}");
        var mediaUrl = ownerView.GetProperty("mediaUrl").GetString()!;
        var claimId = await campus.Claim(requester, itemId, campus.Other);

        // End the original search grant. A pending claim itself is a reason to
        // keep showing the item, including its photo, until the decision.
        using (var scope = campus.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
            var grant = await db.SearchGrants.SingleAsync(g => g.ItemId == itemId && g.UserId == campus.Other);
            grant.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            await db.SaveChangesAsync();
        }
        using (var pendingPhoto = await requester.GetAsync(mediaUrl))
            Assert.Equal(HttpStatusCode.OK, pendingPhoto.StatusCode);
        var claimBefore = await requester.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{claimId}");
        var version = claimBefore.GetProperty("claim").GetProperty("version").GetInt32();

        using var rejected = await owner.PostAsJsonAsync($"/api/v1/claims/{claimId}/reject",
            new { version, reason = "TEST ONLY: insufficient ownership evidence" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);

        // The requester can still track the decision and read its reason.
        var claimAfter = await requester.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{claimId}");
        Assert.Equal("rejected", claimAfter.GetProperty("claim").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, claimAfter.GetProperty("item").GetProperty("mediaUrl").ValueKind);
        var itemAfter = await requester.GetFromJsonAsync<JsonElement>($"/api/v1/found/{itemId}");
        Assert.Equal(JsonValueKind.Null, itemAfter.GetProperty("mediaUrl").ValueKind);
        using var listResponse = await requester.GetAsync("/api/v1/found?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var historyItem = listDocument.RootElement.GetProperty("items").EnumerateArray()
            .Single(item => item.GetProperty("id").GetGuid() == itemId);
        Assert.Equal(JsonValueKind.Null, historyItem.GetProperty("mediaUrl").ValueKind);

        // A previously copied direct image URL must be denied independently of
        // whether the user can still see the historical claim or item details.
        using (var requesterPhoto = await requester.GetAsync(mediaUrl))
            Assert.Equal(HttpStatusCode.Forbidden, requesterPhoto.StatusCode);
        using (var ownerPhoto = await owner.GetAsync(mediaUrl))
            Assert.Equal(HttpStatusCode.OK, ownerPhoto.StatusCode);
        using (var adminPhoto = await admin.GetAsync(mediaUrl))
            Assert.Equal(HttpStatusCode.OK, adminPhoto.StatusCode);
    }

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
