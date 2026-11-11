using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class HostingSecurityTests
{
    private static IConfiguration Config(string? changedKey = null, string? changedValue = null)
    {
        var settings = new Dictionary<string, string?> {
            ["PublicOrigin"] = "https://campus.example.test", ["AllowedHosts"] = "campus.example.test",
            ["Storage:Local"] = "false", ["Storage:Url"] = "https://project.example.test", ["Storage:Key"] = "not-a-real-secret",
            ["Email:Provider"] = "resend", ["Email:From"] = "verification@example.test", ["Email:ResendKey"] = "not-a-real-secret",
            ["DataProtection:Directory"] = "/var/lib/smart-campus/keys", ["DataProtection:CertificatePath"] = "/run/secrets/keys.pfx",
            ["Release:Mode"] = "controlled", ["Release:AllowedEmails:0"] = "authorized-test@o6u.edu.eg",
            ["ConnectionStrings:Campus"] = "Host=database.example.test;Database=campus;Username=runtime;Password=not-a-real-secret;SSL Mode=VerifyFull"
        };
        if (changedKey is not null) settings[changedKey] = changedValue;
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public void Hosted_configuration_accepts_explicit_https_persistent_storage_and_verified_database_tls() => HostingSecurity.Validate(Config());

    [Theory]
    [InlineData("PublicOrigin", "http://campus.example.test")]
    [InlineData("PublicOrigin", "https://localhost")]
    [InlineData("PublicOrigin", "https://campus.example.test/path")]
    [InlineData("PublicOrigin", "https://user:secret@campus.example.test")]
    [InlineData("AllowedHosts", "*")]
    [InlineData("AllowedHosts", "different.example.test")]
    [InlineData("Storage:Local", "true")]
    [InlineData("Storage:Key", "")]
    [InlineData("Email:From", "")]
    [InlineData("Email:LocalSink", "true")]
    [InlineData("Email:ResendKey", "")]
    [InlineData("Email:Provider", "typo")]
    [InlineData("Release:Mode", "typo")]
    [InlineData("DataProtection:CertificatePath", "")]
    [InlineData("ConnectionStrings:Campus", "Host=database.example.test;SSL Mode=Require")]
    public void Unsafe_hosted_configuration_is_rejected_without_exposing_values(string key, string value)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => HostingSecurity.Validate(Config(key, value)));
        Assert.Contains(key, failure.Message);
        Assert.DoesNotContain("not-a-real-secret", failure.Message);
    }

    [Fact]
    public void Brevo_requires_private_api_key_and_explicitly_confirmed_sender()
    {
        var values = Config().AsEnumerable()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        values["Email:Provider"] = "brevo";
        values["Email:BrevoKey"] = "synthetic-brevo-key";
        values["Email:BrevoSenderVerified"] = "true";
        HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        values["Email:BrevoKey"] = null;
        Assert.Contains("Email:BrevoKey", Assert.Throws<InvalidOperationException>(() =>
            HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build())).Message);
        values["Email:BrevoKey"] = "synthetic-brevo-key";
        values["Email:BrevoSenderVerified"] = "false";
        Assert.Contains("Email:BrevoSenderVerified", Assert.Throws<InvalidOperationException>(() =>
            HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build())).Message);
    }

    [Fact]
    public void EmailJS_requires_private_credentials_and_explicit_trial_approval()
    {
        var values = Config().AsEnumerable()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        values["Email:Provider"] = "emailjs";
        values["Email:EmailJsServiceId"] = "test-service";
        values["Email:EmailJsTemplateId"] = "test-template";
        values["Email:EmailJsPublicKey"] = "test-public";
        values["Email:EmailJsPrivateKey"] = "test-private";
        values["Email:EmailJsTrialApproved"] = "true";
        HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        values["Email:EmailJsPrivateKey"] = null;
        Assert.Contains("Email:EmailJsPrivateKey", Assert.Throws<InvalidOperationException>(() =>
            HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build())).Message);
        values["Email:EmailJsPrivateKey"] = "test-private";
        values["Email:EmailJsTrialApproved"] = null;
        Assert.Contains("Email:EmailJsTrialApproved", Assert.Throws<InvalidOperationException>(() =>
            HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build())).Message);
    }

    [Fact]
    public void Open_university_release_requires_explicit_operational_approval_reference()
    {
        var failure = Assert.Throws<InvalidOperationException>(() => HostingSecurity.Validate(Config("Release:Mode", "open")));
        Assert.Contains("Release:UniversityApproval", failure.Message);
    }

    [Fact]
    public void Hosted_database_key_store_requires_certificate_without_persistent_local_files()
    {
        var values = Config().AsEnumerable()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        values["DataProtection:Store"] = "database";
        values["DataProtection:Directory"] = null;
        values["DataProtection:CertificatePath"] = null;
        values["DataProtection:CertificateBase64"] = "certificate-provided-in-host-secret-manager";
        values["DataProtection:CertificatePassword"] = "private-value";
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        HostingSecurity.Validate(config);

        values["DataProtection:CertificateBase64"] = null;
        var missing = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Contains("DataProtection:CertificateBase64",
            Assert.Throws<InvalidOperationException>(() => HostingSecurity.Validate(missing)).Message);
        values["DataProtection:Store"] = "unknown";
        Assert.Contains("DataProtection:Store",
            Assert.Throws<InvalidOperationException>(() => HostingSecurity.Validate(
                new ConfigurationBuilder().AddInMemoryCollection(values).Build())).Message);
    }

    [Fact]
    public void MonsterAsp_private_key_ring_requires_explicit_host_platform()
    {
        var values = Config().AsEnumerable().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        values["DataProtection:Store"] = "monsterasp-private";
        values["DataProtection:CertificatePath"] = null;
        values["DataProtection:CertificateBase64"] = null;
        values["DataProtection:CertificatePassword"] = null;
        values["DataProtection:Directory"] = null;
        values["Hosting:Platform"] = "monsterasp";
        HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        values["Hosting:Platform"] = "snapdeploy";
        var failure = Assert.Throws<InvalidOperationException>(() =>
            HostingSecurity.Validate(new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        Assert.Contains("Hosting:Platform", failure.Message);
        Assert.DoesNotContain("not-a-real-secret", failure.Message);
    }

    [Fact]
    public void MonsterAsp_private_key_ring_is_not_permitted_without_Windows_host()
    {
        if (OperatingSystem.IsWindows()) return;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Hosting:Platform"] = "monsterasp",
            ["DataProtection:Store"] = "monsterasp-private"
        }).Build();
        Assert.Throws<InvalidOperationException>(() => HostingSecurity.ConfigureKeys(
            new ServiceCollection(), configuration, development: false));
    }

    [DatabaseFact]
    public void Encrypted_database_keys_survive_restart_without_local_volume()
    {
        var connectionString = Environment.GetEnvironmentVariable("SMART_CAMPUS_TEST_DB")!;
        var parsed = new NpgsqlConnectionStringBuilder(connectionString);
        if (parsed.Database?.EndsWith("_test", StringComparison.Ordinal) != true ||
            parsed.Host is not ("localhost" or "127.0.0.1"))
            throw new InvalidOperationException("Data Protection integration test requires a loopback _test database.");

        var dbOptions = new DbContextOptionsBuilder<CampusDb>()
            .UseNpgsql(connectionString, options => options.MigrationsHistoryTable("__EFMigrationsHistory", "smart_campus")).Options;
        using (var db = new CampusDb(dbOptions)) db.Database.Migrate();

        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=SmartCampus Database Key Test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var password = "synthetic-test-only-pfx-pass";
        var base64 = Convert.ToBase64String(certificate.Export(X509ContentType.Pfx, password));
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Campus"] = connectionString,
            ["DataProtection:Store"] = "database",
            ["DataProtection:CertificateBase64"] = base64,
            ["DataProtection:CertificatePassword"] = password
        }).Build();

        string protectedValue;
        var firstServices = new ServiceCollection();
        HostingSecurity.ConfigureKeys(firstServices, configuration, development: false);
        using (var first = firstServices.BuildServiceProvider())
            protectedValue = first.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("render-ephemeral-restart").Protect("test-ticket-no-secrets");

        using (var connection = new NpgsqlConnection(connectionString))
        {
            connection.Open();
            using var cmd = new NpgsqlCommand(
                "SELECT \"Xml\" FROM smart_campus.\"DataProtectionKeys\" ORDER BY \"FriendlyName\" DESC LIMIT 1", connection);
            var saved = Assert.IsType<string>(cmd.ExecuteScalar());
            Assert.Contains("encryptedSecret", saved);
            Assert.DoesNotContain("test-ticket-no-secrets", saved);
        }

        var secondServices = new ServiceCollection();
        HostingSecurity.ConfigureKeys(secondServices, configuration, development: false);
        using var second = secondServices.BuildServiceProvider();
        Assert.Equal("test-ticket-no-secrets", second.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("render-ephemeral-restart").Unprotect(protectedValue));
    }

    [Fact]
    public void Certificate_encrypted_keys_decrypt_existing_payload_after_provider_restart()
    {
        var directory = Path.Combine(Path.GetTempPath(), "smart-campus-key-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=SmartCampus Test Only", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var certificatePath = Path.Combine(directory, "test-only.pfx");
        const string password = "test-only-certificate-password";
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx, password));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["DataProtection:Directory"] = Path.Combine(directory, "keys"), ["DataProtection:CertificatePath"] = certificatePath,
            ["DataProtection:CertificatePassword"] = password
        }).Build();
        string protectedPayload;
        var firstServices = new ServiceCollection();
        HostingSecurity.ConfigureKeys(firstServices, config, development: false);
        using (var first = firstServices.BuildServiceProvider())
            protectedPayload = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test").Protect("synthetic-session-ticket");
        var keyFile = Assert.Single(Directory.GetFiles(Path.Combine(directory, "keys"), "key-*.xml"));
        Assert.Contains("encryptedSecret", File.ReadAllText(keyFile));
        var secondServices = new ServiceCollection();
        HostingSecurity.ConfigureKeys(secondServices, config, development: false);
        using var second = secondServices.BuildServiceProvider();
        Assert.Equal("synthetic-session-ticket", second.GetRequiredService<IDataProtectionProvider>().CreateProtector("restart-test").Unprotect(protectedPayload));
    }
}
