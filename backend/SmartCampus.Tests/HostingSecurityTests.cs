using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    [InlineData("DataProtection:CertificatePath", "")]
    [InlineData("ConnectionStrings:Campus", "Host=database.example.test;SSL Mode=Require")]
    public void Unsafe_hosted_configuration_is_rejected_without_exposing_values(string key, string value)
    {
        var failure = Assert.Throws<InvalidOperationException>(() => HostingSecurity.Validate(Config(key, value)));
        Assert.Contains(key, failure.Message);
        Assert.DoesNotContain("not-a-real-secret", failure.Message);
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
