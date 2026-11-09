using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public static class Bootstrap
{
    public static async Task Admin(IServiceProvider services, IConfiguration config)
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { "user", "maintenance", "department", "admin" })
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new(role)));
        var users = services.GetRequiredService<UserManager<CampusUser>>();
        var email = Rules.UniversityEmail(config["Bootstrap:Email"] ?? "");
        var user = await users.FindByEmailAsync(email);
        if (user is null || !user.EmailConfirmed) throw new RuleException("Register and verify the bootstrap account first.");
        Ensure(await users.AddToRoleAsync(user, "admin"));
    }
    private static void Ensure(IdentityResult outcome)
    {
        if (!outcome.Succeeded) throw new InvalidOperationException(string.Join(",", outcome.Errors.Select(e => e.Code)));
    }
}
