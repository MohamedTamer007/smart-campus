using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public static class HostingSecurity
{
    public static void Validate(IConfiguration config)
    {
        var invalid = new List<string>();
        var origin = config["PublicOrigin"];
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.IsLoopback ||
            uri.AbsolutePath != "/" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            invalid.Add("PublicOrigin");
        var hosts = config["AllowedHosts"]?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (hosts.Length == 0 || hosts.Any(host => host.Contains('*')) || uri is null || !hosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
            invalid.Add("AllowedHosts");
        if (!bool.TryParse(config["Storage:Local"], out var localStorage) || localStorage) invalid.Add("Storage:Local");
        if (!Uri.TryCreate(config["Storage:Url"], UriKind.Absolute, out var storage) || storage.Scheme != "https" || storage.UserInfo.Length != 0)
            invalid.Add("Storage:Url");
        foreach (var key in new[] { "Storage:Key", "Email:From" })
            if (string.IsNullOrWhiteSpace(config[key])) invalid.Add(key);
        var keyStore = config["DataProtection:Store"] ?? "file";
        if (keyStore == "database")
        {
            if (string.IsNullOrWhiteSpace(config["DataProtection:CertificateBase64"])) invalid.Add("DataProtection:CertificateBase64");
            if (string.IsNullOrWhiteSpace(config["DataProtection:CertificatePassword"])) invalid.Add("DataProtection:CertificatePassword");
        }
        else if (keyStore == "file")
        {
            foreach (var key in new[] { "DataProtection:Directory", "DataProtection:CertificatePath" })
                if (string.IsNullOrWhiteSpace(config[key])) invalid.Add(key);
        }
        else if (keyStore == "monsterasp-private")
        {
            // MonsterASP.NET documents a persistent, writable private folder
            // next to the public web root, even on the free Windows plan.
            // No user profile, DPAPI or PFX import is available/reliable there.
            if (config["Hosting:Platform"] != "monsterasp") invalid.Add("Hosting:Platform");
        }
        else invalid.Add("DataProtection:Store");
        if (config.GetValue<bool>("Email:LocalSink")) invalid.Add("Email:LocalSink");
        var provider = config["Email:Provider"] ?? "smtp";
        if (provider == "resend")
        {
            if (string.IsNullOrWhiteSpace(config["Email:ResendKey"])) invalid.Add("Email:ResendKey");
        }
        else if (provider == "smtp")
        {
            if (string.IsNullOrWhiteSpace(config["Email:Host"])) invalid.Add("Email:Host");
        }
        else if (provider == "emailjs")
        {
            foreach (var key in new[] { "Email:EmailJsServiceId", "Email:EmailJsTemplateId",
                "Email:EmailJsPublicKey", "Email:EmailJsPrivateKey" })
                if (string.IsNullOrWhiteSpace(config[key])) invalid.Add(key);
            // EmailJS has no provider delivery receipt. Do not activate its
            // backend mail channel without explicitly reviewing this limitation.
            if (!config.GetValue<bool>("Email:EmailJsTrialApproved")) invalid.Add("Email:EmailJsTrialApproved");
        }
        else if (provider == "brevo")
        {
            if (string.IsNullOrWhiteSpace(config["Email:BrevoKey"])) invalid.Add("Email:BrevoKey");
            // Real sender verification/domain ownership is an external provider gate.
            // Do not enable without a deliberate review of the authorized sender.
            if (!config.GetValue<bool>("Email:BrevoSenderVerified")) invalid.Add("Email:BrevoSenderVerified");
        }
        else invalid.Add("Email:Provider");
        var releaseMode = config["Release:Mode"] ?? "controlled";
        if (releaseMode is not ("controlled" or "open")) invalid.Add("Release:Mode");
        if (releaseMode == "open" && string.IsNullOrWhiteSpace(config["Release:UniversityApproval"])) invalid.Add("Release:UniversityApproval");
        if (releaseMode == "controlled")
        {
            var allowed = config.GetSection("Release:AllowedEmails").Get<string[]>() ?? [];
            if (allowed.Length == 0 || allowed.Any(string.IsNullOrWhiteSpace)) invalid.Add("Release:AllowedEmails");
            else
            {
                try { foreach (var email in allowed) Rules.UniversityEmail(email); }
                catch (RuleException) { invalid.Add("Release:AllowedEmails"); }
            }
        }
        try
        {
            var database = new NpgsqlConnectionStringBuilder(config.GetConnectionString("Campus"));
            if (string.IsNullOrWhiteSpace(database.Host) || database.SslMode != SslMode.VerifyFull)
                invalid.Add("ConnectionStrings:Campus (verified TLS required)");
        }
        catch (ArgumentException) { invalid.Add("ConnectionStrings:Campus"); }
        if (invalid.Count != 0) throw new InvalidOperationException("Hosted configuration missing or invalid: " + string.Join(", ", invalid));
    }

    public static void ConfigureKeys(IServiceCollection services, IConfiguration config, bool development)
    {
        var protection = services.AddDataProtection().SetApplicationName("SmartCampus");
        var keyStore = config["DataProtection:Store"] ?? "file";
        X509Certificate2? certificate = null;
        if (keyStore == "database")
        {
            var connectionString = config.GetConnectionString("Campus")
                ?? throw new InvalidOperationException("A database connection is required for persistent Data Protection keys.");
            var encoded = config["DataProtection:CertificateBase64"];
            if (string.IsNullOrWhiteSpace(encoded))
                throw new InvalidOperationException("An X.509 certificate is required for database-backed Data Protection keys.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { throw new InvalidOperationException("The Data Protection certificate encoding is invalid."); }
            try
            {
                certificate = X509CertificateLoader.LoadPkcs12(bytes, config["DataProtection:CertificatePassword"],
                    X509KeyStorageFlags.EphemeralKeySet);
            }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
            services.Configure<KeyManagementOptions>(options =>
                options.XmlRepository = new PostgresKeyRepository(connectionString));
        }
        else if (keyStore == "monsterasp-private")
        {
            if (!OperatingSystem.IsWindows() || config["Hosting:Platform"] != "monsterasp")
                throw new InvalidOperationException("MonsterASP private key storage requires the approved Windows host.");
            // The website is installed at .../wwwroot and /private is its
            // host-provided nonpublic, persistent sibling (not an HTTP route).
            var privateKeys = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "private", "keys"));
            protection.PersistKeysToFileSystem(new DirectoryInfo(privateKeys));
            // MonsterASP.NET Free has no app-pool Windows user profile, so
            // ProtectKeysWithDpapi() throws. Their documented private folder
            // provides filesystem-only access control to the XML key ring.
        }
        else if (keyStore == "file")
        {
            var directory = config["DataProtection:Directory"] ?? "local-data/keys";
            protection.PersistKeysToFileSystem(new DirectoryInfo(directory));
            var certificatePath = config["DataProtection:CertificatePath"];
            if (!string.IsNullOrWhiteSpace(certificatePath))
                certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                    config["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
            else if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
            else if (!development) throw new InvalidOperationException("Hosted Data Protection certificate is required");
        }
        else throw new InvalidOperationException("Unsupported Data Protection persistence mode.");
        if (certificate is not null)
        {
            if (!certificate.HasPrivateKey) throw new InvalidOperationException("Data Protection certificate requires a private key");
            protection.ProtectKeysWithCertificate(certificate);
        }
    }
}
