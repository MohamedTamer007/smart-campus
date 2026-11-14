using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed class PrivateMedia(IHttpClientFactory clients, IConfiguration config, IHostEnvironment environment)
{
    private string LocalRoot => Path.GetFullPath(config["Storage:LocalDirectory"] ?? "local-data/media");
    private bool Local => environment.IsDevelopment() && config.GetValue<bool>("Storage:Local");

    public async Task<MediaObject> Upload(IFormFile file, MediaObject metadata)
    {
        if (file.Length is <= 0 or > CampusImageNormalizer.MaximumUploadBytes) throw new RuleException("invalid_upload");
        using var input = file.OpenReadStream();
        using var bounded = new MemoryStream();
        await input.CopyToAsync(bounded);
        var jpeg = CampusImageNormalizer.ToJpeg(bounded.ToArray());
        metadata.ObjectKey = "smart-campus/" + metadata.Id.ToString("N") + ".jpg";
        await Write(metadata, jpeg);
        return metadata;
    }

    private HttpClient Client()
    {
        if (!Uri.TryCreate(config["Storage:Url"], UriKind.Absolute, out var url) || url.Scheme != "https" || string.IsNullOrWhiteSpace(config["Storage:Key"]))
            throw new RuleException("storage_unavailable", 503);
        var client = clients.CreateClient("storage");
        client.BaseAddress = new Uri(url, "/storage/v1/");
        var key = config["Storage:Key"]!;
        // New Supabase sb_secret_* API keys are opaque, not JWTs.
        // Sending "Authorization: Bearer sb_secret_*" causes invalid-JWT errors.
        // The apikey header is sufficient for backend-to-Storage calls.
        if (!key.StartsWith("sb_secret_", StringComparison.Ordinal))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        else
            client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Remove("apikey");
        client.DefaultRequestHeaders.Add("apikey", key);
        return client;
    }

    private string LocalPath(MediaObject metadata) => Path.Combine(LocalRoot, metadata.Bucket, metadata.Id.ToString("N") + ".jpg");

    private async Task Write(MediaObject metadata, byte[] bytes)
    {
        if (Local)
        {
            var path = LocalPath(metadata);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes);
            return;
        }
        using var client = Client();
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new("image/jpeg");
        using var response = await client.PostAsync($"object/{metadata.Bucket}/{metadata.ObjectKey}", content);
        if (!response.IsSuccessStatusCode) throw new RuleException("storage_unavailable", 503);
    }

    public async Task<byte[]> Read(MediaObject metadata)
    {
        if (Local) return await File.ReadAllBytesAsync(LocalPath(metadata));
        using var client = Client();
        using var response = await client.GetAsync($"object/{metadata.Bucket}/{metadata.ObjectKey}");
        if (!response.IsSuccessStatusCode) throw new RuleException("storage_unavailable", 503);
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task Delete(MediaObject metadata)
    {
        if (Local) { File.Delete(LocalPath(metadata)); return; }
        using var client = Client();
        using var response = await client.DeleteAsync($"object/{metadata.Bucket}/{metadata.ObjectKey}");
        if (!response.IsSuccessStatusCode) throw new RuleException("storage_unavailable", 503);
    }
}
