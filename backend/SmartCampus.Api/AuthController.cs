using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

namespace SmartCampus.Api;

public sealed record RegisterInput(string Name, string Email, string Password, string Confirmation);
public sealed record LoginInput(string Email, string Password);
public sealed record EmailInput(string Email);
public sealed record TokenInput(string UserId, string Token);
public sealed record ResetInput(string UserId, string Token, string Password, string Confirmation);
public sealed record PasswordInput(string CurrentPassword, string NewPassword);
public sealed record ProfileInput(string Name, string Language);

[ApiController, Route("api/v1/auth"), EnableRateLimiting("auth")]
public sealed class AuthController(UserManager<CampusUser> users, SignInManager<CampusUser> signin,
    CampusDb db, Access access, EmailSender mail, IConfiguration config, ReleasePolicy release) : ControllerBase
{
    [HttpGet("csrf")]
    public object Csrf(IAntiforgery antiforgery) => new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken };

    [HttpPost("register")]
    public async Task<object> Register(RegisterInput input)
    {
        release.Require(Rules.UniversityEmail(input.Email));
        if (input.Password != input.Confirmation || input.Password.Length > 128) throw new RuleException("invalid_password");
        var user = new CampusUser { Id = Guid.NewGuid(), UserName = Rules.UniversityEmail(input.Email),
            Email = Rules.UniversityEmail(input.Email), DisplayName = Rules.Required(input.Name, 150) };
        Check(await users.CreateAsync(user, input.Password));
        await SendVerification(user);
        return new { verificationRequired = true };
    }

    [HttpPost("login"), ProducesResponseType<AccountResponse>(200)]
    public async Task<object> Login(LoginInput input)
    {
        var email = Rules.UniversityEmail(input.Email);
        release.Require(email);
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.Active) throw new RuleException("credentials", 401);
        if (!user.EmailConfirmed) throw new RuleException("unverified", 403);
        var outcome = await signin.PasswordSignInAsync(user, input.Password, false, true);
        db.Audit.Add(new() { ActorId = user.Id, Action = outcome.Succeeded ? "Login" : "LoginFailed", EntityId = user.Id });
        await db.SaveChangesAsync();
        if (!outcome.Succeeded) throw new RuleException("credentials", 401);
        return await Account(user);
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout() { await signin.SignOutAsync(); return NoContent(); }

    [HttpGet("me"), Authorize, ProducesResponseType<AccountResponse>(200)]
    public async Task<object> Me()
    {
        var actor = await access.Current();
        return await Account((await users.FindByIdAsync(actor.Id.ToString()))!);
    }

    [HttpPost("resend")]
    public async Task<object> Resend(EmailInput input)
    {
        var email = Rules.UniversityEmail(input.Email);
        if (!release.Allows(email)) return new { accepted = true };
        var user = await users.FindByEmailAsync(email);
        if (user is { EmailConfirmed: false }) await SendVerification(user);
        return new { accepted = true };
    }

    [HttpPost("verify")]
    public async Task<object> Verify(TokenInput input)
    {
        if (!Guid.TryParse(input.UserId, out _)) throw new RuleException("invalid_token");
        var user = await users.FindByIdAsync(input.UserId);
        if (user is null || user.EmailConfirmed) throw new RuleException("invalid_token");
        if (!release.Allows(user.Email!)) throw new RuleException("invalid_token");
        Check(await users.ConfirmEmailAsync(user, Decode(input.Token)));
        return new { verified = true };
    }

    [HttpPost("recover")]
    public async Task<object> Recover(EmailInput input)
    {
        var email = Rules.UniversityEmail(input.Email);
        if (!release.Allows(email)) return new { accepted = true };
        var user = await users.FindByEmailAsync(email);
        if (user is { EmailConfirmed: true, Active: true })
        {
            var token = Encode(await users.GeneratePasswordResetTokenAsync(user));
            // IIS rejects oversized URL path segments. Keep the token in the query string.
            await mail.Enqueue(user.Email!, "Smart Campus password reset",
                $"{Origin()}/reset?userId={user.Id}&token={Uri.EscapeDataString(token)}");
        }
        return new { accepted = true };
    }

    [HttpPost("reset")]
    public async Task<object> Reset(ResetInput input)
    {
        if (!Guid.TryParse(input.UserId, out _)) throw new RuleException("invalid_token");
        if (input.Password != input.Confirmation || input.Password.Length > 128) throw new RuleException("invalid_password");
        var user = await users.FindByIdAsync(input.UserId) ?? throw new RuleException("invalid_token");
        if (!release.Allows(user.Email!)) throw new RuleException("invalid_token");
        Check(await users.ResetPasswordAsync(user, Decode(input.Token), input.Password));
        await db.Sessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync();
        return new { reset = true };
    }

    [HttpPut("profile"), Authorize, ProducesResponseType<AccountResponse>(200)]
    public async Task<object> Profile(ProfileInput input)
    {
        var actor = await access.Current();
        var user = (await users.FindByIdAsync(actor.Id.ToString()))!;
        user.DisplayName = Rules.Required(input.Name, 150);
        if (input.Language is not ("ar" or "en")) throw new RuleException("invalid_input");
        user.Language = input.Language;
        Check(await users.UpdateAsync(user));
        return await Account(user);
    }

    [HttpPost("password"), Authorize]
    public async Task<IActionResult> Password(PasswordInput input)
    {
        var actor = await access.Current();
        var user = (await users.FindByIdAsync(actor.Id.ToString()))!;
        if (input.NewPassword.Length > 128) throw new RuleException("invalid_password");
        Check(await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword));
        await db.Sessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync();
        await signin.SignOutAsync();
        return NoContent();
    }

    public sealed record DeleteAccountInput(string Password);

    [HttpDelete("account"), Authorize]
    public async Task<IActionResult> DeleteAccount(DeleteAccountInput input)
    {
        var actor = await access.Current();
        var user = await users.FindByIdAsync(actor.Id.ToString()) ?? throw new RuleException("expired", 401);
        if (string.IsNullOrEmpty(input.Password) || !await users.CheckPasswordAsync(user, input.Password))
            throw new RuleException("credentials", 400);

        // Preserve referential integrity of maintenance/lost-property history: erase
        // login credentials and identifying profile data, leave an anonymous
        // disabled tombstone for records that still reference this user ID.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await mail.ForgetPendingFor(user.Email!);
        await db.Sessions.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        await db.Notifications.Where(x => x.RecipientId == user.Id).ExecuteDeleteAsync();
        await db.SearchGrants.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        await db.Scopes.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserRoles.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserClaims.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserLogins.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();
        await db.UserTokens.Where(x => x.UserId == user.Id).ExecuteDeleteAsync();

        user.UserName = "deleted-" + user.Id.ToString("N");
        user.NormalizedUserName = user.UserName.ToUpperInvariant();
        user.Email = null;
        user.NormalizedEmail = null;
        user.EmailConfirmed = false;
        user.PasswordHash = null;
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.TwoFactorEnabled = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.ConcurrencyStamp = Guid.NewGuid().ToString("N");
        user.DisplayName = "Deleted account";
        user.Language = "ar";
        user.Active = false;
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        await signin.SignOutAsync();
        return NoContent();
    }

    private async Task<object> Account(CampusUser user)
    {
        var roles = (await users.GetRolesAsync(user)).ToArray();
        var departments = await db.Scopes.Where(s => s.UserId == user.Id).Join(db.Departments, s => s.DepartmentId, d => d.Id,
            (s, d) => new { id = d.Id, code = d.Code }).ToArrayAsync();
        return new { id = user.Id, name = user.DisplayName, email = user.Email, roles, departments,
            role = roles.Contains("admin") ? "admin" : roles.Contains("maintenance") ? "maintenance" : roles.Contains("department") ? "department" : "user",
            department = departments.FirstOrDefault()?.code, language = user.Language };
    }

    private async Task SendVerification(CampusUser user)
    {
        var token = Encode(await users.GenerateEmailConfirmationTokenAsync(user));
        await mail.Enqueue(user.Email!, "Verify your Smart Campus email", $"{Origin()}/verify?userId={user.Id}&token={token}");
    }
    private string Origin() => (config["PublicOrigin"] ?? throw new RuleException("email_unavailable", 503)).TrimEnd('/');
    private static string Encode(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    private static string Decode(string token)
    {
        try { return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException) { throw new RuleException("invalid_token"); }
    }
    private static void Check(IdentityResult outcome)
    {
        if (!outcome.Succeeded) throw new RuleException(outcome.Errors.Any(e => e.Code.Contains("Token")) ? "invalid_token" : "invalid_input");
    }
}
