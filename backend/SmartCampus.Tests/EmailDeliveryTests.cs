using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class EmailDeliveryTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class MailBoundary(HttpStatusCode status) : HttpMessageHandler, IHttpClientFactory
    {
        public int Requests { get; private set; }
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"id\":\"synthetic-provider-message\"}") });
        }
    }
    private WebApplicationFactory<Program> Host(MailBoundary boundary) => campus.WithWebHostBuilder(builder => {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
            ["Email:Provider"] = "resend", ["Email:LocalSink"] = "false", ["Email:ResendKey"] = "synthetic-no-real-provider-key",
            ["Email:From"] = "Smart Campus testing <onboarding@resend.dev>", ["Email:TestRecipient"] = campus.Email(campus.Student)
        }));
        builder.ConfigureServices(services => services.AddSingleton<IHttpClientFactory>(boundary));
    });
    private async Task<OutboundEmail> Queue(IServiceProvider services, CampusDb db)
    {
        var previous = await db.OutboundEmails.Select(email => email.Id).ToArrayAsync();
        await services.GetRequiredService<EmailSender>().Enqueue(campus.Email(campus.Student), "Synthetic verification", "http://localhost/verify?token=synthetic-private-token");
        return await db.OutboundEmails.SingleAsync(email => !previous.Contains(email.Id));
    }

    [DatabaseFact]
    public async Task Accepted_email_retains_provider_correlation_and_does_not_resend_after_commit()
    {
        using var boundary = new MailBoundary(HttpStatusCode.OK);
        using var host = Host(boundary);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var email = await Queue(scope.ServiceProvider, db);
        Assert.DoesNotContain("synthetic-private-token", email.ProtectedPayload);
        await scope.ServiceProvider.GetRequiredService<EmailSender>().Deliver(email.Id);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.OutboundEmails.SingleAsync(record => record.Id == email.Id);
        Assert.Equal("synthetic-provider-message", stored.ProviderMessageId);
        Assert.NotNull(stored.SentAt);
        Assert.Null(stored.FailureCode);
        Assert.Equal("", stored.ProtectedPayload);
        await scope.ServiceProvider.GetRequiredService<EmailSender>().Deliver(email.Id);
        Assert.Equal(1, boundary.Requests);
    }

    [DatabaseFact]
    public async Task Rejected_email_never_records_acceptance_and_keeps_only_sanitized_failure_metadata()
    {
        using var boundary = new MailBoundary(HttpStatusCode.UnprocessableEntity);
        using var host = Host(boundary);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var email = await Queue(scope.ServiceProvider, db);
        await Assert.ThrowsAsync<RuleException>(() => scope.ServiceProvider.GetRequiredService<EmailSender>().Deliver(email.Id));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var stored = await db.OutboundEmails.SingleAsync(record => record.Id == email.Id);
        Assert.Null(stored.ProviderMessageId);
        Assert.Null(stored.SentAt);
        Assert.Equal("email_rejected", stored.FailureCode);
        Assert.DoesNotContain("synthetic-private-token", stored.ProtectedPayload);
        Assert.NotEmpty(stored.ProtectedPayload);
    }
}
