using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>Restricts a public testing deployment to explicitly authorized university accounts.</summary>
public sealed class ReleasePolicy(IConfiguration config)
{
    public bool Allows(string email)
    {
        var mode = config["Release:Mode"] ?? "controlled";
        if (mode == "open") return true;
        if (mode != "controlled") throw new InvalidOperationException("Unknown Release:Mode");
        return (config.GetSection("Release:AllowedEmails").Get<string[]>() ?? [])
            .Any(allowed => Rules.UniversityEmail(allowed).Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    public void Require(string email)
    {
        if (!Allows(email)) throw new RuleException("release_restricted", 403);
    }
}
