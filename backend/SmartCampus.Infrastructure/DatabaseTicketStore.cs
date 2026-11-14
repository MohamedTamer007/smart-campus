using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SmartCampus.Infrastructure;

public sealed class DatabaseTicketStore(IServiceScopeFactory scopes, IDataProtectionProvider protection) : ITicketStore
{
    private readonly IDataProtector protector = protection.CreateProtector("SmartCampus.SessionTicket.v1");
    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Convert.FromHexString(key)));

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        db.Sessions.Add(new() { Id = Hash(key), UserId = Guid.Parse(ticket.Principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
            ProtectedTicket = protector.Protect(Convert.ToBase64String(TicketSerializer.Default.Serialize(ticket))),
            ExpiresAt = ticket.Properties.ExpiresUtc!.Value, AbsoluteExpiry = DateTimeOffset.UtcNow.AddHours(8) });
        await db.SaveChangesAsync();
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var id = Hash(key);
        var payload = protector.Protect(Convert.ToBase64String(TicketSerializer.Default.Serialize(ticket)));
        var expiry = ticket.Properties.ExpiresUtc!.Value;
        var now = DateTimeOffset.UtcNow;
        // Update only: concurrent logout/revocation must win without recreating a ticket or failing the response.
        await db.Sessions.Where(session => session.Id == id && session.ExpiresAt > now && session.AbsoluteExpiry > now)
            .ExecuteUpdateAsync(update => update.SetProperty(session => session.ProtectedTicket, payload)
                .SetProperty(session => session.ExpiresAt, session => expiry < session.AbsoluteExpiry ? expiry : session.AbsoluteExpiry));
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (key.Length != 64 || !key.All(Uri.IsHexDigit)) return null;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        var session = await db.Sessions.FindAsync(Hash(key));
        if (session is null || session.ExpiresAt <= DateTimeOffset.UtcNow || session.AbsoluteExpiry <= DateTimeOffset.UtcNow) return null;
        try { return TicketSerializer.Default.Deserialize(Convert.FromBase64String(protector.Unprotect(session.ProtectedTicket))); }
        catch (CryptographicException) { return null; }
    }

    public async Task RemoveAsync(string key)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CampusDb>();
        await db.Sessions.Where(s => s.Id == Hash(key)).ExecuteDeleteAsync();
    }
}
