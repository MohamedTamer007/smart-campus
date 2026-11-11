using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>Limits campus access either to test accounts or to verified university-domain addresses.</summary>
public sealed class ReleasePolicy(IConfiguration config)
{
    public bool Allows(string email)
    {
        var mode = config["Release:Mode"] ?? "controlled";
        if (mode == "open") return true;
        if (mode == "university")
        {
            // Allow all university-domain accounts without maintaining a manual
            // email allowlist. Authentication still requires email confirmation.
            try { Rules.UniversityEmail(email); return true; }
            catch (RuleException) { return false; }
        }
        if (mode != "controlled") throw new InvalidOperationException("Unknown Release:Mode");
        return (config.GetSection("Release:AllowedEmails").Get<string[]>() ?? [])
            .Any(allowed => Rules.UniversityEmail(allowed).Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public void Require(string email)
    {
        if (!Allows(email)) throw new RuleException("release_restricted", 403);
    }
}
