using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class ReleaseTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private WebApplicationFactory<Program> Controlled(params string[] emails) => campus.WithWebHostBuilder(builder =>
        builder.ConfigureAppConfiguration((_, config) => {
            var settings = emails.Select((email, index) => new KeyValuePair<string, string?>("Release:AllowedEmails:" + index, email)).ToDictionary();
            settings["Release:Mode"] = "controlled";
            config.AddInMemoryCollection(settings);
        }));

    [Theory]
    [InlineData("student@o6u.edu.eg", true)]
    [InlineData("STUDENT@O6U.EDU.EG", true)]
    [InlineData("student@gmail.com", false)]
    [InlineData("student@o6u.edu.eg.attacker.example", false)]
    public void University_mode_allows_only_the_exact_campus_email_domain(string email, bool expected)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Release:Mode"] = "university"
        }).Build();
        Assert.Equal(expected, new ReleasePolicy(config).Allows(email));
    }

    [Theory]
    [InlineData("student@o6u.edu.eg", true)]
    [InlineData("student@o6u.edu.eg.attacker.example", false)]
    public void Default_release_policy_accepts_all_and_only_exact_university_addresses(string email, bool expected)
    {
        var config = new ConfigurationBuilder().Build();
        Assert.Equal(expected, new ReleasePolicy(config).Allows(email));
    }

    [DatabaseFact]
    public async Task University_mode_registers_any_campus_email_and_requires_confirmation_before_login()
    {
        using var host = campus.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Release:Mode"] = "university",
                ["Release:AllowedEmails:0"] = null,
                ["Release:UniversityApproval"] = null
            })));
        using var client = host.CreateClient();
        await CampusFixture.Csrf(client);
        var email = "any-campus-" + Guid.NewGuid().ToString("N") + "@o6u.edu.eg";
        var beforeMail = Directory.Exists(campus.MailDirectory) ? Directory.GetFiles(campus.MailDirectory, "*.eml") : [];
        var response = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/register", new {
            name = "Synthetic new campus student", email, password = campus.Password, confirmation = campus.Password
        }));
        Assert.True(response.GetProperty("verificationRequired").GetBoolean());
        var sentMail = Directory.GetFiles(campus.MailDirectory, "*.eml").Except(beforeMail).ToArray();
        Assert.Single(sentMail);
        var message = await MimeKit.MimeMessage.LoadAsync(sentMail[0]);
        Assert.Equal(email, message.To.Mailboxes.Single().Address);
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = campus.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("unverified", await login.Content.ReadAsStringAsync());

        var externalEmail = "external-" + Guid.NewGuid().ToString("N") + "@other.example";
        var rejected = await client.PostAsJsonAsync("/api/v1/auth/register", new {
            name = "Not a campus student", email = externalEmail, password = campus.Password, confirmation = campus.Password
        });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [DatabaseFact]
    public async Task Controlled_release_blocks_unlisted_registration_and_login()
    {
        using var host = Controlled(campus.Email(campus.Student));
        using var client = host.CreateClient();
        await CampusFixture.Csrf(client);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var before = await db.Users.CountAsync();
        var registration = await client.PostAsJsonAsync("/api/v1/auth/register", new {
            name = "Synthetic unlisted account", email = "unlisted-" + Guid.NewGuid().ToString("N") + "@o6u.edu.eg", password = campus.Password, confirmation = campus.Password
        });
        Assert.Equal(HttpStatusCode.Forbidden, registration.StatusCode);
        Assert.Equal(before, await db.Users.CountAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(campus.Staff), password = campus.Password })).StatusCode);
    }

    [DatabaseFact]
    public async Task Controlled_release_keeps_unlisted_recovery_and_resend_generic_without_sending()
    {
        using var host = Controlled(campus.Email(campus.Student));
        using var client = host.CreateClient();
        await CampusFixture.Csrf(client);
        var before = Directory.Exists(campus.MailDirectory) ? Directory.GetFiles(campus.MailDirectory).Length : 0;
        foreach (var route in new[] { "recover", "resend" })
        {
            var known = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/" + route, new { email = campus.Email(campus.Staff) }));
            var unknown = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/" + route, new { email = "unknown-" + Guid.NewGuid().ToString("N") + "@o6u.edu.eg" }));
            Assert.True(known.GetProperty("accepted").GetBoolean());
            Assert.Equal(known.GetRawText(), unknown.GetRawText());
        }
        Assert.Equal(before, Directory.Exists(campus.MailDirectory) ? Directory.GetFiles(campus.MailDirectory).Length : 0);
    }

    [DatabaseFact]
    public async Task Authorized_testing_account_can_register_and_still_requires_verification()
    {
        var email = "authorized-" + Guid.NewGuid().ToString("N") + "@o6u.edu.eg";
        using var host = Controlled(email.ToUpperInvariant());
        using var client = host.CreateClient();
        await CampusFixture.Csrf(client);
        var registration = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/register", new {
            name = "Synthetic authorized account", email, password = campus.Password, confirmation = campus.Password
        }));
        Assert.True(registration.GetProperty("verificationRequired").GetBoolean());
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = campus.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        Assert.Contains("unverified", await login.Content.ReadAsStringAsync());
    }

    [DatabaseFact]
    public async Task Removing_testing_authorization_blocks_existing_session_but_preserves_logout()
    {
        using var host = Controlled(campus.Email(campus.Staff));
        using var client = host.CreateClient();
        await CampusFixture.Csrf(client);
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(campus.Staff), password = campus.Password }));
        await CampusFixture.Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/maintenance")).StatusCode);
        host.Services.GetRequiredService<IConfiguration>()["Release:AllowedEmails:0"] = campus.Email(campus.Student);
        var denied = await client.GetAsync("/api/v1/maintenance");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains("release_restricted", await denied.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }
}
