using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class ApiTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
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
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/verify", verification)).StatusCode);
        var login = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = campus.Password }));
        Assert.Equal("user", login.GetProperty("role").GetString());
        await CampusFixture.Csrf(client);
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/recover", new { email }));
        link = await MailLink(email, "/reset/");
        var uri = new Uri(link);
        var reset = new { userId = verification.userId, token = uri.AbsolutePath.Split('/').Last(), password = campus.Password + "changed", confirmation = campus.Password + "changed" };
        await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/reset", reset));
        await CampusFixture.Csrf(client);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/auth/reset", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        var unknown = await CampusFixture.Response(await client.PostAsJsonAsync("/api/v1/auth/recover", new { email = "nonexistent@o6u.edu.eg" }));
        Assert.True(unknown.GetProperty("accepted").GetBoolean());
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
