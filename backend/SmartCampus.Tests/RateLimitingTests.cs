using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace SmartCampus.Tests;

public sealed class RateLimitingTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Session_and_csrf_reads_preserve_credential_budget_while_failed_logins_are_limited()
    {
        using var host = campus.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimits:AuthPerMinute"] = "2" })));
        using var client = host.CreateClient();
        for (var index = 0; index < 4; index++)
        {
            await CampusFixture.Csrf(client);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        }
        var input = new { email = Guid.NewGuid().ToString("N") + "@o6u.edu.eg", password = "Synthetic invalid credential" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", input)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", input)).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/v1/auth/login", input)).StatusCode);
        await CampusFixture.Csrf(client);
    }
}
