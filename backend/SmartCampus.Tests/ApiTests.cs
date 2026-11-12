using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class ApiTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    private sealed class SyntheticAiHttp : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls { get; private set; }
        public HttpClient CreateClient(string _) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            Assert.Equal("https://integrate.api.nvidia.com/v1/chat/completions", request.RequestUri!.ToString());
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            Assert.Equal("openai/gpt-oss-20b", payload.RootElement.GetProperty("model").GetString());
            var textOnly = payload.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
            Assert.Contains("TEST ONLY", textOnly);
            if (payload.RootElement.TryGetProperty("reasoning_effort", out var effort))
            {
                Assert.Equal("low", effort.GetString());
                Assert.False(payload.RootElement.TryGetProperty("response_format", out _));
                Assert.Contains(payload.RootElement.GetProperty("max_tokens").GetInt32(), new[] { 1024, 2048 });
                Assert.False(payload.RootElement.GetProperty("stream").GetBoolean());
            }
            const string body = """{"choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"low\",\"risk\":\"low\",\"explanation\":\"Synthetic only\"}"}}]}""";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        }
    }

    [DatabaseFact]
    public async Task Student_smoke_is_invisible_and_unavailable_without_separate_opt_in()
    {
        using var student = await campus.Login(campus.Student);
        var status = await student.GetFromJsonAsync<JsonElement>("/api/v1/ai/smoke");
        Assert.False(status.GetProperty("enabled").GetBoolean());
        var response = await student.PostAsJsonAsync("/api/v1/ai/smoke", new { attemptedPrivatePayload = "must never be processed" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [DatabaseFact]
    public async Task Student_in_controlled_test_release_can_smoke_once_with_only_fixed_fictional_text()
    {
        using var boundary = new SyntheticAiHttp();
        using var host = campus.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Release:Mode"] = "controlled",
                ["Release:AllowedEmails:0"] = campus.Email(campus.Student),
                ["AI:Provider"] = "auto",
                ["AI:SyntheticSmokeTestsEnabled"] = "true",
                ["AI:SyntheticUserSmokeTestsEnabled"] = "true",
                ["AI:AllowNonSensitiveInference"] = "true",
                ["AI:AllowNonSensitiveImages"] = "false",
                ["AI:NvidiaKey"] = "synthetic-test-only-not-real",
                ["AI:OpenRouterKey"] = "synthetic-test-only-not-real",
                ["AI:NvidiaDailyRequestLimit"] = "5",
                ["AI:NvidiaRequestsPerMinute"] = "5",
                ["AI:FaultTypes:0"] = "plumbing",
                ["AI:Priorities:0"] = "low",
                ["AI:Risks:0"] = "low"
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(boundary);
            });
        });
        using var student = host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await CampusFixture.Csrf(student);
        var login = await student.PostAsJsonAsync("/api/v1/auth/login",
            new { email = campus.Email(campus.Student), password = campus.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await CampusFixture.Csrf(student);

        var status = await student.GetFromJsonAsync<JsonElement>("/api/v1/ai/smoke");
        Assert.True(status.GetProperty("enabled").GetBoolean());

        var first = await CampusFixture.Response(await student.PostAsJsonAsync("/api/v1/ai/smoke", new { attemptedUserPrompt = "must not reach provider" }));
        Assert.Equal("validated", first.GetProperty("status").GetString());
        Assert.Equal("nvidia", first.GetProperty("provider").GetString());
        Assert.Equal("openai/gpt-oss-20b", first.GetProperty("model").GetString());
        Assert.Equal(3, first.EnumerateObject().Count());
        Assert.Equal(1, boundary.Calls);

        var repeat = await student.PostAsJsonAsync("/api/v1/ai/smoke", new { });
        Assert.Equal(HttpStatusCode.TooManyRequests, repeat.StatusCode);
        Assert.Equal(1, boundary.Calls);
    }

    [DatabaseFact]
    public async Task Student_smoke_requires_valid_login_and_csrf()
    {
        using var guest = campus.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/ai/smoke")).StatusCode);
        using var student = await campus.Login(campus.Student);
        student.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await student.PostAsJsonAsync("/api/v1/ai/smoke", new { })).StatusCode);
    }

    // These integration tests share one isolated fixture DB and one UTC-day cap.
    // Never delete or change a live database: only the _test fixture reaches here.
    private async Task ResetAdminDiagnosticCounter()
    {
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        await db.InferenceUsage.Where(u => u.Provider == "admin-synthetic-smoke").ExecuteDeleteAsync();
    }

    [DatabaseFact]
    public async Task Synthetic_smoke_test_requires_explicit_admin_diagnostic_enablement()
    {
        using var host = campus.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration(
            (_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["AI:AdminSyntheticDiagnosticEnabled"] = "false"
            })));
        using var admin = host.CreateClient();
        await CampusFixture.Csrf(admin);
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new {
            email = campus.Email(campus.Admin), password = campus.Password
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await CampusFixture.Csrf(admin);
        var response = await admin.PostAsJsonAsync("/api/v1/admin/operations/ai-smoke", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("ai_smoke_disabled", await response.Content.ReadAsStringAsync());
    }

    [DatabaseFact]
    public async Task Synthetic_smoke_uses_only_fixed_text_and_one_budgeted_admin_request()
    {
        await ResetAdminDiagnosticCounter();
        using var boundary = new SyntheticAiHttp();
        using var host = campus.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["AI:Provider"] = "auto",
                ["AI:AdminSyntheticDiagnosticEnabled"] = "true",
                ["AI:AllowNonSensitiveInference"] = "false",
                ["AI:AllowNonSensitiveImages"] = "false",
                ["AI:NvidiaKey"] = "synthetic-test-only-not-real",
                ["AI:OpenRouterKey"] = "synthetic-test-only-not-real",
                ["AI:NvidiaDailyRequestLimit"] = "5",
                ["AI:NvidiaRequestsPerMinute"] = "5",
                // Deliberately omit actual maintenance taxonomy: synthetic diagnostic must still work.
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(boundary);
            });
        });
        using var admin = host.CreateClient();
        await CampusFixture.Csrf(admin);
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(campus.Admin), password = campus.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await CampusFixture.Csrf(admin);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/operations/ai-smoke", new { });
        var result = await CampusFixture.Response(response);
        Assert.Equal("validated", result.GetProperty("status").GetString());
        Assert.Equal("nvidia", result.GetProperty("provider").GetString());
        Assert.Equal("openai/gpt-oss-20b", result.GetProperty("model").GetString());
        Assert.Equal(3, result.EnumerateObject().Count());
        Assert.Equal(1, boundary.Calls);
        // Keep the mocked HTTP handler dedicated to the synthetic NVIDIA call.
        // The operations endpoint also probes a retrieval worker via IHttpClientFactory.
        using var scope = host.Services.CreateScope();
        var currentConfig = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        Assert.True(currentConfig.GetValue<bool>("AI:AdminSyntheticDiagnosticEnabled"));
        Assert.False(currentConfig.GetValue<bool>("AI:AllowNonSensitiveInference"));
        for (var i = 0; i < 2; i++)
            Assert.Equal(HttpStatusCode.OK,
                (await admin.PostAsJsonAsync("/api/v1/admin/operations/ai-smoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await admin.PostAsJsonAsync("/api/v1/admin/operations/ai-smoke", new { })).StatusCode);
        Assert.Equal(3, boundary.Calls);
    }

    [DatabaseFact]
    public async Task Admin_real_AI_demo_classifies_only_fixed_fictional_data()
    {
        await ResetAdminDiagnosticCounter();
        using var boundary = new SyntheticAiHttp();
        using var host = campus.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["AI:AdminSyntheticDiagnosticEnabled"] = "true",
                ["AI:AllowNonSensitiveInference"] = "false",
                ["AI:NvidiaKey"] = "synthetic-test-only-not-real",
                ["AI:NvidiaDailyRequestLimit"] = "5",
                ["AI:NvidiaRequestsPerMinute"] = "5"
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(boundary);
            });
        });
        using var admin = host.CreateClient();
        await CampusFixture.Csrf(admin);
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login",
            new { email = campus.Email(campus.Admin), password = campus.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await CampusFixture.Csrf(admin);
        var result = await CampusFixture.Response(await admin.PostAsJsonAsync(
            "/api/v1/admin/operations/ai-demo",
            new { attemptedPersonalData = "should never reach NVIDIA" }));
        Assert.Equal("validated", result.GetProperty("status").GetString());
        Assert.Equal("nvidia", result.GetProperty("provider").GetString());
        Assert.Equal("plumbing", result.GetProperty("type").GetString());
        Assert.Equal("low", result.GetProperty("priority").GetString());
        Assert.Equal("low", result.GetProperty("risk").GetString());
        Assert.Equal(1, boundary.Calls);
        Assert.DoesNotContain("should never reach NVIDIA", result.GetRawText());
    }

    [DatabaseFact]
    public async Task Non_admin_cannot_trigger_smoke_inference()
    {
        using var staff = await campus.Login(campus.Staff);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin/operations/ai-smoke", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin/operations/ai-demo", new { })).StatusCode);
    }

    [DatabaseFact]
    public async Task Operational_diagnostics_reject_guests_users_and_scoped_staff()
    {
        using var guest = campus.CreateClient();
        using var user = await campus.Login(campus.Student);
        using var staff = await campus.Login(campus.Staff);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/admin/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/admin/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync("/api/v1/admin/operations")).StatusCode);
    }

    [DatabaseFact]
    public async Task Administrator_operational_summary_contains_real_counts_without_private_payloads()
    {
        using var student = await campus.Login(campus.Student);
        await campus.Report(student);
        using var administrator = await campus.Login(campus.Admin);
        var response = await CampusFixture.Response(await administrator.GetAsync("/api/v1/admin/operations"));
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        Assert.Equal(await db.Jobs.CountAsync(), response.GetProperty("jobs").EnumerateArray().Sum(j => j.GetProperty("count").GetInt32()));
        Assert.Equal(8, response.EnumerateObject().Count());
        var ai = response.GetProperty("ai");
        Assert.Equal("openrouter", ai.GetProperty("provider").GetString());
        Assert.False(ai.GetProperty("textConfigured").GetBoolean());
        Assert.False(ai.GetProperty("imagesConfigured").GetBoolean());
        Assert.False(ai.GetProperty("taxonomyConfigured").GetBoolean());
        Assert.False(ai.GetProperty("textTransferEnabled").GetBoolean());
        Assert.False(ai.GetProperty("imageTransferEnabled").GetBoolean());
        Assert.False(ai.GetProperty("nvidiaKeyPresent").GetBoolean());
        Assert.False(ai.GetProperty("openRouterKeyPresent").GetBoolean());
        Assert.False(ai.GetProperty("nvidiaBudgetConfigured").GetBoolean());
        Assert.True(ai.GetProperty("openRouterBudgetConfigured").GetBoolean());
        Assert.True(response.GetProperty("workingSetBytes").GetInt64() > 0);
        Assert.Contains(response.GetProperty("worker").GetString(), new[] { "available", "unavailable" });
        Assert.Equal(0, response.GetProperty("quotas").EnumerateArray().Single(q => q.GetProperty("provider").GetString() == "nvidia").GetProperty("dailyLimit").GetInt32());
        Assert.DoesNotContain(campus.Email(campus.Student), response.GetRawText());
        Assert.DoesNotContain("ProtectedPayload", response.GetRawText());
        Assert.DoesNotContain("Host=", response.GetRawText());
    }

    [DatabaseFact]
    public async Task Unauthenticated_unverified_and_forged_privileges_are_denied()
    {
        using var guest = campus.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/maintenance")).StatusCode);
        await CampusFixture.Csrf(guest);
        var login = await guest.PostAsJsonAsync("/api/v1/auth/login", new { email = campus.Email(campus.Unverified), password = campus.Password });
        Assert.Equal(HttpStatusCode.Forbidden, login.StatusCode);
        using var user = await campus.Login(campus.Student);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync("/api/v1/admin/accounts")).StatusCode);
        var grants = await user.PutAsJsonAsync($"/api/v1/admin/accounts/{campus.Student}/grants", new { roles = new[] { "admin" }, departmentIds = Array.Empty<Guid>(), version = "forged" });
        Assert.Equal(HttpStatusCode.Forbidden, grants.StatusCode);
    }

    [DatabaseFact]
    public async Task Missing_csrf_is_rejected_and_logout_revokes_session()
    {
        using var client = await campus.Login(campus.Student);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        await CampusFixture.Csrf(client);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [DatabaseFact]
    public async Task Maintenance_scope_stale_updates_and_lifecycle_are_enforced()
    {
        using var student = await campus.Login(campus.Student);
        using var other = await campus.Login(campus.Other);
        using var staff = await campus.Login(campus.Staff);
        using var outsider = await campus.Login(campus.Outsider);
        var id = await campus.Report(student);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/maintenance/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/maintenance/{id}")).StatusCode);
        var own = await student.GetFromJsonAsync<JsonElement>($"/api/v1/maintenance/{id}");
        Assert.Equal(JsonValueKind.Null, own.GetProperty("assessment").ValueKind);
        var review = await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/maintenance/{id}/updates", new { version = 1, status = "review", assignee = campus.Staff, note = "Reviewed" }));
        Assert.False(review.GetProperty("canEdit").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await student.PostAsJsonAsync($"/api/v1/maintenance/{id}/cancel", new { version = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync($"/api/v1/maintenance/{id}/updates", new { version = 1, status = "repair", note = "Stale" })).StatusCode);
        await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/maintenance/{id}/updates", new { version = 2, status = "repair", assignee = campus.Staff, note = "Repair started" }));
        var resolved = await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/maintenance/{id}/updates", new { version = 3, status = "resolved", note = "Fixed" }));
        Assert.Equal("resolved", resolved.GetProperty("status").GetString());
        Assert.Equal(4, resolved.GetProperty("history").GetArrayLength());
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync($"/api/v1/maintenance/{id}/updates", new { version = 4, status = "review", note = "Reopen" })).StatusCode);
    }

    [DatabaseFact]
    public async Task Prehandling_cancellation_and_rejection_preserve_truthful_states()
    {
        using var student = await campus.Login(campus.Student);
        using var staff = await campus.Login(campus.Staff);
        var cancelled = await campus.Report(student);
        var response = await CampusFixture.Response(await student.PostAsJsonAsync($"/api/v1/maintenance/{cancelled}/cancel", new { version = 1 }));
        Assert.Equal("submitted", response.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, response.GetProperty("cancelledAt").ValueKind);
        var rejected = await campus.Report(student);
        await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/maintenance/{rejected}/updates", new { version = 1, status = "review", note = "Review" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.PostAsJsonAsync($"/api/v1/maintenance/{rejected}/updates", new { version = 2, status = "rejected", note = "" })).StatusCode);
        response = await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/maintenance/{rejected}/updates", new { version = 2, status = "rejected", note = "Duplicate report" }));
        Assert.Equal("rejected", response.GetProperty("status").GetString());
    }

    [DatabaseFact]
    public async Task Private_inventory_media_and_uncalibrated_search_do_not_leak()
    {
        using var uploader = await campus.Login(campus.Student);
        using var other = await campus.Login(campus.Other);
        var id = await campus.Item(uploader);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/v1/found/{id}")).StatusCode);
        var own = await uploader.GetFromJsonAsync<JsonElement>($"/api/v1/found/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync(own.GetProperty("mediaUrl").GetString())).StatusCode);
        var list = await other.GetFromJsonAsync<JsonElement>("/api/v1/found");
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), f => f.GetProperty("id").GetGuid() == id);
        using var search = new MultipartFormDataContent { { new StringContent("Blue distinctive bottle"), "description" } };
        var unavailable = await other.PostAsync("/api/v1/found/search", search);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("search_calibration_required", await unavailable.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/v1/found/{id}/claims", new { message = "Guessed ID" })).StatusCode);
    }

    [DatabaseFact]
    public async Task Custody_changes_only_on_scoped_receipt_and_former_custodian_loses_authority()
    {
        using var uploader = await campus.Login(campus.Student);
        using var staff = await campus.Login(campus.Staff);
        using var outsider = await campus.Login(campus.Outsider);
        var id = await campus.Item(uploader);
        var handover = await CampusFixture.Response(await uploader.PostAsJsonAsync($"/api/v1/found/{id}/handovers", new { version = 1, department = campus.Department, office = "TEST ONLY desk" }));
        Assert.Equal(campus.Student, handover.GetProperty("custodianId").GetGuid());
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync($"/api/v1/found/{id}/handovers/confirm", new { version = 2 })).StatusCode);
        var notReceived = await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/found/{id}/handovers/not-received", new { version = 2 }));
        Assert.Equal(campus.Student, notReceived.GetProperty("custodianId").GetGuid());
        var received = await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/found/{id}/handovers/confirm", new { version = 3 }));
        Assert.Equal(JsonValueKind.Null, received.GetProperty("custodianId").ValueKind);
        Assert.Equal("test-maintenance", received.GetProperty("custodianDepartment").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await uploader.PostAsJsonAsync($"/api/v1/found/{id}/handovers/cancel", new { version = 4 })).StatusCode);
    }

    [DatabaseFact]
    public async Task Concurrent_approvals_create_one_reservation_and_both_confirmations_close_it()
    {
        using var uploader = await campus.Login(campus.Student);
        using var first = await campus.Login(campus.Other);
        using var second = await campus.Login(campus.Outsider);
        using var staff = await campus.Login(campus.Staff);
        using var staffTwo = await campus.Login(campus.StaffTwo);
        var id = await campus.Item(uploader);
        await CampusFixture.Response(await uploader.PostAsJsonAsync($"/api/v1/found/{id}/handovers", new { version = 1, department = campus.Department, office = "TEST ONLY desk" }));
        await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/found/{id}/handovers/confirm", new { version = 2 }));
        var firstClaim = await campus.Claim(first, id, campus.Other);
        var secondClaim = await campus.Claim(second, id, campus.Outsider);
        Assert.Equal(HttpStatusCode.Conflict, (await first.PostAsJsonAsync($"/api/v1/found/{id}/claims", new { message = "Duplicate" })).StatusCode);
        var decisions = await Task.WhenAll(staff.PostAsJsonAsync($"/api/v1/claims/{firstClaim}/approve", new { version = 1, place = "Test desk", instructions = "Bring evidence" }),
            staffTwo.PostAsJsonAsync($"/api/v1/claims/{secondClaim}/approve", new { version = 1, place = "Test desk", instructions = "Bring evidence" }));
        Assert.Single(decisions, r => r.IsSuccessStatusCode);
        Assert.Single(decisions, r => r.StatusCode == HttpStatusCode.Conflict);
        var winner = decisions[0].IsSuccessStatusCode ? firstClaim : secondClaim;
        var requester = decisions[0].IsSuccessStatusCode ? first : second;
        var competitor = decisions[0].IsSuccessStatusCode ? second : first;
        var losing = decisions[0].IsSuccessStatusCode ? secondClaim : firstClaim;
        Assert.Equal(HttpStatusCode.Forbidden, (await uploader.PostAsJsonAsync($"/api/v1/claims/{winner}/approve", new { version = 2, phone = "01012345678" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await competitor.GetAsync($"/api/v1/claims/{winner}")).StatusCode);
        var one = await CampusFixture.Response(await requester.PostAsJsonAsync($"/api/v1/claims/{winner}/confirm", new { version = 2 }));
        Assert.Equal("approved", one.GetProperty("status").GetString());
        Assert.True(one.GetProperty("claimantConfirmed").GetBoolean());
        Assert.False(one.GetProperty("custodianConfirmed").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync($"/api/v1/claims/{winner}/cancel", new { version = 3, reason = "Too late" })).StatusCode);
        var both = await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/claims/{winner}/confirm", new { version = 3 }));
        Assert.Equal("closed", both.GetProperty("status").GetString());
        Assert.True(both.GetProperty("custodianConfirmed").GetBoolean());
        Assert.True(both.GetProperty("claimantConfirmed").GetBoolean());
        var repeated = await CampusFixture.Response(await requester.PostAsJsonAsync($"/api/v1/claims/{winner}/confirm", new { version = 2 }));
        Assert.Equal(4, repeated.GetProperty("version").GetInt32());
        var remaining = await competitor.GetFromJsonAsync<JsonElement>($"/api/v1/claims/{losing}");
        Assert.Equal("ItemRecovered", remaining.GetProperty("claim").GetProperty("closureReason").GetString());
        var found = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/found/{id}");
        Assert.Equal("delivered", found.GetProperty("state").GetString());
    }

    [DatabaseFact]
    public async Task Reservation_blocks_transfer_and_cancellation_releases_existing_claims()
    {
        using var holder = await campus.Login(campus.Student);
        using var requester = await campus.Login(campus.Other);
        using var staff = await campus.Login(campus.Staff);
        var id = await campus.Item(holder);
        await CampusFixture.Response(await holder.PostAsJsonAsync($"/api/v1/found/{id}/handovers", new { version = 1, department = campus.Department, office = "Test office" }));
        var claim = await campus.Claim(requester, id, campus.Other);
        await CampusFixture.Response(await holder.PostAsJsonAsync($"/api/v1/claims/{claim}/approve", new { version = 1, phone = "01012345678" }));
        Assert.Equal(HttpStatusCode.Conflict, (await staff.PostAsJsonAsync($"/api/v1/found/{id}/handovers/confirm", new { version = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await holder.PostAsJsonAsync($"/api/v1/found/{id}/handovers", new { version = 3, department = campus.SecondDepartment, office = "Test" })).StatusCode);
        await CampusFixture.Response(await holder.PostAsJsonAsync($"/api/v1/claims/{claim}/cancel", new { version = 2, reason = "Wrong owner" }));
        var released = await CampusFixture.Response(await staff.PostAsJsonAsync($"/api/v1/found/{id}/handovers/confirm", new { version = 4 }));
        Assert.Equal("available", released.GetProperty("state").GetString());
    }

    [DatabaseFact]
    public async Task Invalid_upload_and_inconsistent_location_are_rejected()
    {
        using var client = await campus.Login(campus.Student);
        using var invalid = new MultipartFormDataContent { { new StringContent("Test"), "description" }, { new StringContent("Test"), "place" },
            { new StringContent("2026-01-01"), "date" }, { new StringContent("Test"), "storage" }, { new StringContent("<script>alert(1)</script>"), "photo", "evil.png" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/found", invalid)).StatusCode);
        using var wrong = new MultipartFormDataContent { { new StringContent("Test"), "description" }, { new StringContent("Test"), "details" },
            { new StringContent(campus.Floor.ToString()), "faculty" }, { new StringContent(campus.Building.ToString()), "building" }, { new StringContent(campus.Floor.ToString()), "floor" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/maintenance", wrong)).StatusCode);
    }

    [DatabaseFact]
    public async Task Registration_verification_recovery_and_replayed_tokens_follow_real_flows()
    {
        using var client = campus.CreateClient();
        await CampusFixture.Csrf(client);
        var email = Guid.NewGuid().ToString("N") + "@o6u.edu.eg";
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/register", new { name = "New Student", email, password = campus.Password, confirmation = campus.Password, role = "admin" }));
        var link = await MailLink(email, "/verify?");
        var parameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(link).Query);
        var verification = new { userId = parameters["userId"].ToString(), token = parameters["token"].ToString() };
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/verify", verification));
        var repeatedVerification = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/verify", verification));
        Assert.True(repeatedVerification.GetProperty("alreadyVerified").GetBoolean());
        var login = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = campus.Password }));
        Assert.Equal("user", login.GetProperty("role").GetString());
        await CampusFixture.Csrf(client);
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/recover", new { email }));
        link = await MailLink(email, "/reset?");
        var uri = new Uri(link);
        Assert.Equal("/reset", uri.AbsolutePath);
        var resetParameters = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var reset = new { userId = resetParameters["userId"].ToString(), token = resetParameters["token"].ToString(), password = campus.Password + "changed", confirmation = campus.Password + "changed" };
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/reset", reset));
        await CampusFixture.Csrf(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/reset", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        var unknown = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/recover", new { email = "nonexistent@o6u.edu.eg" }));
        Assert.True(unknown.GetProperty("accepted").GetBoolean());
    }

    [DatabaseFact]
    public async Task Deleting_own_account_removes_identity_and_sessions_but_preserves_anonymous_report_history()
    {
        var accountId = Guid.NewGuid();
        var address = "delete-" + accountId.ToString("N") + "@o6u.edu.eg";
        var reportId = Guid.NewGuid();
        using (var setup = campus.Services.CreateScope())
        {
            var users = setup.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CampusUser>>();
            var result = await users.CreateAsync(new CampusUser {
                Id = accountId, UserName = address, Email = address,
                EmailConfirmed = true, DisplayName = "Delete me" }, campus.Password);
            Assert.True(result.Succeeded);
            var database = setup.ServiceProvider.GetRequiredService<CampusDb>();
            database.Reports.Add(new SmartCampus.Domain.MaintenanceReport {
                Id = reportId, ReporterId = accountId,
                FacultyId = campus.Faculty, BuildingId = campus.Building, FloorId = campus.Floor,
                DepartmentId = campus.Department, Description = "Synthetic history"
            });
            await database.SaveChangesAsync();
        }

        using var client = campus.CreateClient();
        await CampusFixture.Csrf(client);
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/login", new { email = address, password = campus.Password }));
        await CampusFixture.Csrf(client);
        using var wrongRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account") {
            Content = JsonContent.Create(new { password = "wrong-password" })
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(wrongRequest)).StatusCode);
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account") {
            Content = JsonContent.Create(new { password = campus.Password })
        };
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(deleteRequest)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);

        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var deleted = await db.Users.SingleAsync(x => x.Id == accountId);
        Assert.False(deleted.Active);
        Assert.Null(deleted.Email);
        Assert.Null(deleted.PasswordHash);
        Assert.Equal("Deleted account", deleted.DisplayName);
        Assert.False(await db.Sessions.AnyAsync(x => x.UserId == accountId));
        Assert.True(await db.Reports.AnyAsync(x => x.Id == reportId && x.ReporterId == accountId));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [DatabaseFact]
    public async Task Expired_database_session_is_rejected_even_with_a_cookie()
    {
        using var client = await campus.Login(campus.Student);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        await db.Sessions.Where(s => s.UserId == campus.Student).ExecuteUpdateAsync(update => update.SetProperty(s => s.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [DatabaseFact]
    public async Task Admin_grants_are_versioned_and_revoke_existing_sessions()
    {
        var target = Guid.NewGuid();
        using (var scope = campus.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CampusUser>>();
            Assert.True((await users.CreateAsync(new() { Id = target, UserName = campus.Email(target), Email = campus.Email(target), DisplayName = "Grant target", EmailConfirmed = true }, campus.Password)).Succeeded);
        }
        using var admin = await campus.Login(campus.Admin);
        using var member = await campus.Login(target);
        var accounts = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/accounts");
        var version = accounts.GetProperty("items").EnumerateArray().Single(entry => entry.GetProperty("key").GetGuid() == target).GetProperty("version").GetString();
        var grants = new { roles = new[] { "department" }, departmentIds = new[] { campus.Department }, version };
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/accounts/{target}/grants", grants)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await member.GetAsync("/api/v1/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/admin/accounts/{target}/grants", grants)).StatusCode);
        using var refreshed = await campus.Login(target);
        var account = await refreshed.GetFromJsonAsync<JsonElement>("/api/v1/auth/me");
        Assert.Contains(account.GetProperty("roles").EnumerateArray(), role => role.GetString() == "department");
        Assert.Contains(account.GetProperty("departments").EnumerateArray(), department => department.GetProperty("id").GetGuid() == campus.Department);
    }

    [DatabaseFact]
    public async Task Notifications_are_recipient_owned_and_dashboard_counts_use_real_records()
    {
        using var student = await campus.Login(campus.Student);
        using var other = await campus.Login(campus.Other);
        var before = await student.GetFromJsonAsync<JsonElement>("/api/v1/statistics");
        var id = await campus.Report(student);
        var after = await student.GetFromJsonAsync<JsonElement>("/api/v1/statistics");
        Assert.Equal(before.GetProperty("totalReports").GetInt32() + 1, after.GetProperty("totalReports").GetInt32());
        var inbox = await student.GetFromJsonAsync<JsonElement>("/api/v1/notifications?pageSize=100");
        var notification = inbox.GetProperty("items").EnumerateArray().Single(n => n.GetProperty("entityId").GetGuid() == id);
        var notificationId = notification.GetProperty("id").GetGuid();
        Assert.False(notification.GetProperty("read").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/v1/notifications/{notificationId}/read", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsJsonAsync($"/api/v1/notifications/{notificationId}/read", new { })).StatusCode);
        inbox = await student.GetFromJsonAsync<JsonElement>("/api/v1/notifications?pageSize=100");
        Assert.True(inbox.GetProperty("items").EnumerateArray().Single(n => n.GetProperty("id").GetGuid() == notificationId).GetProperty("read").GetBoolean());
    }

    [DatabaseFact]
    public async Task Delivery_first_still_requires_independent_requester_receipt()
    {
        using var holder = await campus.Login(campus.Student);
        using var requester = await campus.Login(campus.Other);
        using var outsider = await campus.Login(campus.Outsider);
        var id = await campus.Item(holder);
        var claim = await campus.Claim(requester, id, campus.Other);
        await CampusFixture.Response(await holder.PostAsJsonAsync($"/api/v1/claims/{claim}/approve", new { version = 1, phone = "01012345678" }));
        Assert.Equal(HttpStatusCode.Conflict, (await holder.PostAsJsonAsync($"/api/v1/claims/{claim}/cancel", new { version = 1, reason = "Stale" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsJsonAsync($"/api/v1/claims/{claim}/confirm", new { version = 2 })).StatusCode);
        var delivered = await CampusFixture.Response(await holder.PostAsJsonAsync($"/api/v1/claims/{claim}/confirm", new { version = 2 }));
        Assert.Equal("approved", delivered.GetProperty("status").GetString());
        Assert.True(delivered.GetProperty("custodianConfirmed").GetBoolean());
        Assert.False(delivered.GetProperty("claimantConfirmed").GetBoolean());
        var closed = await CampusFixture.Response(await requester.PostAsJsonAsync($"/api/v1/claims/{claim}/confirm", new { version = 3 }));
        Assert.Equal("closed", closed.GetProperty("status").GetString());
    }

    [DatabaseFact]
    public async Task Pending_handover_cancellation_keeps_item_and_original_custody()
    {
        using var holder = await campus.Login(campus.Student);
        var id = await campus.Item(holder);
        await CampusFixture.Response(await holder.PostAsJsonAsync($"/api/v1/found/{id}/handovers", new { version = 1, department = campus.Department, office = "Test office" }));
        var cancelled = await CampusFixture.Response(await holder.PostAsJsonAsync($"/api/v1/found/{id}/handovers/cancel", new { version = 2 }));
        Assert.Equal(campus.Student, cancelled.GetProperty("custodianId").GetGuid());
        Assert.Equal("available", cancelled.GetProperty("state").GetString());
        Assert.Equal("cancelled", cancelled.GetProperty("handover").GetProperty("status").GetString());
    }

    [DatabaseFact]
    public async Task Malformed_account_action_tokens_return_validation_errors()
    {
        using var guest = campus.CreateClient();
        await CampusFixture.Csrf(guest);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/verify", new { userId = "not-an-id", token = "invalid" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/reset", new { userId = campus.Student, token = "invalid", password = campus.Password, confirmation = campus.Password })).StatusCode);
    }

    [DatabaseFact]
    public async Task Expired_verification_and_reset_tokens_are_rejected()
    {
        using var guest = campus.CreateClient();
        await CampusFixture.Csrf(guest);
        using var scope = campus.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<CampusUser>>();
        var emailOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailConfirmationOptions>>().Value;
        var resetOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>>().Value;
        var emailLifespan = emailOptions.TokenLifespan;
        var resetLifespan = resetOptions.TokenLifespan;
        var unverified = (await users.FindByIdAsync(campus.Unverified.ToString()))!;
        var verified = (await users.FindByIdAsync(campus.Student.ToString()))!;
        var emailToken = await users.GenerateEmailConfirmationTokenAsync(unverified);
        var resetToken = await users.GeneratePasswordResetTokenAsync(verified);
        static string Encode(string token) => Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(token));
        try
        {
            emailOptions.TokenLifespan = TimeSpan.FromTicks(-1);
            resetOptions.TokenLifespan = TimeSpan.FromTicks(-1);
            Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/verify", new { userId = campus.Unverified, token = Encode(emailToken) })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await guest.PostAsJsonAsync("/api/v1/auth/reset", new { userId = campus.Student, token = Encode(resetToken), password = campus.Password, confirmation = campus.Password })).StatusCode);
        }
        finally { emailOptions.TokenLifespan = emailLifespan; resetOptions.TokenLifespan = resetLifespan; }
    }

    private async Task<string> MailLink(string email, string path)
    {
        foreach (var file in Directory.EnumerateFiles(campus.MailDirectory, "*.eml").OrderByDescending(File.GetLastWriteTimeUtc))
        {
            var message = await MimeMessage.LoadAsync(file);
            var text = message.TextBody ?? "";
            if (message.To.Mailboxes.Any(address => address.Address == email) && text.Contains(path)) return text.Trim();
        }
        throw new InvalidOperationException("Expected local email was not delivered");
    }
}
