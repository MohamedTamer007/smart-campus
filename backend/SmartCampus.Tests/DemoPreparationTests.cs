using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class DemoPreparationTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Demo_preparation_endpoints_are_not_exposed_in_the_operational_application()
    {
        using var host = campus.WithWebHostBuilder(_ => { });
        using var admin = host.CreateClient();
        await CampusFixture.Csrf(admin);
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new {
            email = campus.Email(campus.Admin), password = campus.Password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await CampusFixture.Csrf(admin);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/demo/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsJsonAsync("/api/v1/admin/demo/prepare", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/admin/ai-smoke")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsJsonAsync("/api/v1/admin/operations/ai-demo", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PostAsJsonAsync("/api/v1/admin/operations/openrouter-smoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.GetAsync("/api/v1/admin/operations/ai-smoke")).StatusCode);
    }
}
