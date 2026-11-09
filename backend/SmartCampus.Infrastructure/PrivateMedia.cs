using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SkiaSharp;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

public sealed class PrivateMedia(IHttpClientFactory clients, IConfiguration config, IHostEnvironment environment)
{
    private string LocalRoot => Path.GetFullPath(config["Storage:LocalDirectory"] ?? "local-data/media");
    private bool Local => environment.IsDevelopment() && config.GetValue<bool>("Storage:Local");

    public async Task<MediaObject> Upload(IFormFile file, MediaObject metadata)
    {
        if (file.Length is <= 0 or > 5242880) throw new RuleException("invalid_upload");
        using var input = file.OpenReadStream();
        using var bounded = new MemoryStream();
        await input.CopyToAsync(bounded);
        using var encodedSource = SKData.CreateCopy(bounded.ToArray());
        using var codec = SKCodec.Create(encodedSource);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp) ||
            (long)codec.Info.Width * codec.Info.Height > 16000000) throw new RuleException("invalid_upload");
        using var bitmap = SKBitmap.Decode(encodedSource) ?? throw new RuleException("invalid_upload");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        metadata.ObjectKey = "smart-campus/" + metadata.Id.ToString("N") + ".jpg";
        await Write(metadata, encoded.ToArray());
        return metadata;
    }

    private HttpClient Client()
    {
        if (!Uri.TryCreate(config["Storage:Url"], UriKind.Absolute, out var url) || url.Scheme != "https" || string.IsNullOrWhiteSpace(config["Storage:Key"]))
            throw new RuleException("storage_unavailable", 503);
        var client = clients.CreateClient("storage");
        client.BaseAddress = new Uri(url, "/storage/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config["Storage:Key"]);
        client.DefaultRequestHeaders.Add("apikey", config["Storage:Key"]);
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
