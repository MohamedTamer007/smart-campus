using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

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
        foreach (var key in new[] { "Storage:Key", "Email:From", "DataProtection:Directory", "DataProtection:CertificatePath" })
            if (string.IsNullOrWhiteSpace(config[key])) invalid.Add(key);
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
        else invalid.Add("Email:Provider");
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
        var directory = config["DataProtection:Directory"] ?? "local-data/keys";
        var protection = services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(directory)).SetApplicationName("SmartCampus");
        var certificatePath = config["DataProtection:CertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                config["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
            if (!certificate.HasPrivateKey) throw new InvalidOperationException("Data Protection certificate requires a private key");
            protection.ProtectKeysWithCertificate(certificate);
        }
        else if (OperatingSystem.IsWindows()) protection.ProtectKeysWithDpapi();
        else if (!development) throw new InvalidOperationException("Hosted Data Protection certificate is required");
    }
}
