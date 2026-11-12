using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class DemoPreparationTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private WebApplicationFactory<Program> Host(bool enabled) => campus.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Demo:PreparationEnabled"] = enabled.ToString()
        })));

    private async Task<HttpClient> Login(WebApplicationFactory<Program> host, Guid userId)
    {
        var client = host.CreateClient();
        await CampusFixture.Csrf(client);
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new {
            email = campus.Email(userId), password = campus.Password
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        await CampusFixture.Csrf(client);
        return client;
    }

    [DatabaseFact]
    public async Task Demo_preparation_is_disabled_by_default_and_non_admins_cannot_prepare()
    {
        using var disabled = Host(false);
        using var admin = await Login(disabled, campus.Admin);
        var disabledStatus = await CampusFixture.Response(await admin.GetAsync("/api/v1/admin/demo/status"));
        Assert.False(disabledStatus.GetProperty("enabled").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsJsonAsync("/api/v1/admin/demo/prepare", new { })).StatusCode);

        using var enabled = Host(true);
        using var staff = await Login(enabled, campus.Staff);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/demo/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await staff.PostAsJsonAsync("/api/v1/admin/demo/prepare", new { })).StatusCode);
        using var guest = enabled.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/admin/demo/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await guest.PostAsJsonAsync("/api/v1/admin/demo/prepare", new { })).StatusCode);
    }

    [DatabaseFact]
    public async Task Admin_prepares_five_independent_demo_accounts_and_private_recovery_data_once()
    {
        using var host = Host(true);
        using var admin = await Login(host, campus.Admin);
        var ready = await CampusFixture.Response(await admin.GetAsync("/api/v1/admin/demo/status"));
        Assert.True(ready.GetProperty("enabled").GetBoolean());
        Assert.False(ready.GetProperty("alreadyPrepared").GetBoolean());
        var result = await CampusFixture.Response(await admin.PostAsJsonAsync("/api/v1/admin/demo/prepare", new { }));
        Assert.True(result.GetProperty("prepared").GetBoolean());
        Assert.Equal(5, result.GetProperty("credentials").GetArrayLength());
        var credentials = result.GetProperty("credentials").EnumerateArray().ToDictionary(
            x => x.GetProperty("role").GetString()!, x => x.Clone());
        Assert.Equal(new[] { "student", "requester", "maintenance", "department", "admin" }.Order(), credentials.Keys.Order());
        foreach (var credential in credentials.Values)
        {
            Assert.StartsWith("demo.", credential.GetProperty("email").GetString());
            Assert.EndsWith("@o6u.edu.eg", credential.GetProperty("email").GetString());
            Assert.True(credential.GetProperty("created").GetBoolean());
            Assert.True(credential.GetProperty("password").GetString()!.Length >= 24);
        }

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        Assert.Equal(2, await db.Departments.CountAsync(x => x.Code.StartsWith("DEMO ONLY ")));
        Assert.Equal(3, await db.Locations.CountAsync(x => x.En.StartsWith("DEMO ONLY · Fictional ")));
        Assert.Single(await db.Reports.Where(x => x.Description == "DEMO ONLY · Fictional water leak").ToArrayAsync());
        Assert.Equal(2, await db.Items.CountAsync(x => x.Description.StartsWith("DEMO ONLY · Fictional ")));
        var grants = await db.SearchGrants.Where(x => x.UserId == db.Users.Where(u => u.Email == "demo.requester@o6u.edu.eg").Select(u => u.Id).Single())
            .ToArrayAsync();
        Assert.Equal(2, grants.Length);
        Assert.All(grants, grant => Assert.InRange(grant.ExpiresAt - DateTimeOffset.UtcNow,
            TimeSpan.FromDays(29), TimeSpan.FromDays(30)));

        var item = await db.Items.SingleAsync(x => x.Description == "DEMO ONLY · Fictional blue notebook");
        var mediaId = await db.Media.Where(x => x.ItemId == item.Id).Select(x => x.Id).SingleAsync();
        var before = (Accounts: await db.Users.CountAsync(), Items: await db.Items.CountAsync(), Grants: await db.SearchGrants.CountAsync());
        var repeated = await CampusFixture.Response(await admin.PostAsJsonAsync("/api/v1/admin/demo/prepare", new { }));
        Assert.True(repeated.GetProperty("alreadyPrepared").GetBoolean());
        Assert.Equal(0, repeated.GetProperty("credentials").GetArrayLength());
        Assert.Equal(before, (await db.Users.CountAsync(), await db.Items.CountAsync(), await db.SearchGrants.CountAsync()));
        var complete = await CampusFixture.Response(await admin.GetAsync("/api/v1/admin/demo/status"));
        Assert.True(complete.GetProperty("alreadyPrepared").GetBoolean());

        using var requester = host.CreateClient();
        await CampusFixture.Csrf(requester);
        var login = await CampusFixture.Response(await requester.PostAsJsonAsync("/api/v1/auth/login", new {
            email = "demo.requester@o6u.edu.eg", password = credentials["requester"].GetProperty("password").GetString()
        }));
        Assert.True(login.GetProperty("isDemo").GetBoolean());
        await CampusFixture.Csrf(requester);
        var visibleItems = await requester.GetFromJsonAsync<JsonElement>("/api/v1/found");
        var visibleRecords = visibleItems.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, visibleRecords.Length);
        Assert.All(visibleRecords, visible => Assert.StartsWith("DEMO ONLY · Fictional ", visible.GetProperty("description").GetString()));
        var found = await CampusFixture.Response(await requester.GetAsync("/api/v1/found/" + item.Id));
        Assert.True(found.GetProperty("canClaim").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await requester.GetAsync("/api/v1/media/" + mediaId)).StatusCode);

        using var outsider = await campus.Login(campus.Outsider);
        var outsiderItems = await outsider.GetFromJsonAsync<JsonElement>("/api/v1/found");
        Assert.Empty(outsiderItems.GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/v1/found/" + item.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync("/api/v1/media/" + mediaId)).StatusCode);
    }
}
