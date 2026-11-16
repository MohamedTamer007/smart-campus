using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SmartCampus.Tests;

public sealed class NotificationStatusTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Unread_status_is_recipient_scoped_and_clears_only_after_reading()
    {
        using var guest = campus.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/notifications/unread")).StatusCode);
        using var student = await campus.Login(campus.Student);
        using var other = await campus.Login(campus.Other);
        Assert.False(await HasUnread(student));
        var report = await campus.Report(student);
        Assert.True(await HasUnread(student));
        Assert.False(await HasUnread(other));
        var inbox = await student.GetFromJsonAsync<JsonElement>("/api/v1/notifications");
        var id = inbox.GetProperty("items").EnumerateArray().Single(n => n.GetProperty("entityId").GetGuid() == report).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync($"/api/v1/notifications/{id}/read", new { })).StatusCode);
        Assert.True(await HasUnread(student));
        Assert.Equal(HttpStatusCode.NoContent, (await student.PostAsJsonAsync($"/api/v1/notifications/{id}/read", new { })).StatusCode);
        Assert.False(await HasUnread(student));
    }

    private static async Task<bool> HasUnread(HttpClient client) =>
        (await client.GetFromJsonAsync<JsonElement>("/api/v1/notifications/unread")).GetProperty("hasUnread").GetBoolean();
}
