using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class SessionRaceTests(CampusFixture campus) : IClassFixture<CampusFixture>
{
    [DatabaseFact]
    public async Task Logout_wins_a_blocked_cookie_renewal_without_resurrecting_session_or_throwing()
    {
        var store = campus.Services.GetRequiredService<DatabaseTicketStore>();
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, campus.Student.ToString())], IdentityConstants.ApplicationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), new AuthenticationProperties {
            IssuedUtc = DateTimeOffset.UtcNow, ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        }, IdentityConstants.ApplicationScheme);
        var key = await store.StoreAsync(ticket);
        using var scope = campus.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var blocker = await db.Database.SqlQueryRaw<int>("SELECT pg_backend_pid() AS \"Value\"").SingleAsync();
        Assert.Equal(1, await db.Sessions.Where(session => session.UserId == campus.Student).ExecuteDeleteAsync());
        var renewal = store.RenewAsync(key, ticket);
        var blocked = false;
        try
        {
            await using var inspection = new NpgsqlConnection(Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB"));
            await inspection.OpenAsync();
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE @blocker = ANY(pg_blocking_pids(pid)))", inspection);
            command.Parameters.AddWithValue("blocker", blocker);
            var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
            while (!renewal.IsCompleted && DateTimeOffset.UtcNow < deadline)
            {
                if ((bool)(await command.ExecuteScalarAsync())!) { blocked = true; break; }
                await Task.Delay(20);
            }
        }
        finally { await transaction.CommitAsync(); }
        await renewal;
        Assert.True(blocked, "The actual PostgreSQL renewal must encounter the uncommitted session deletion");
        Assert.Null(await store.RetrieveAsync(key));
        Assert.False(await db.Sessions.AnyAsync(session => session.UserId == campus.Student));
    }
}
