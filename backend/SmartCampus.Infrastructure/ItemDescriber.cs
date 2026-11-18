using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SmartCampus.Domain;

namespace SmartCampus.Infrastructure;

/// <summary>Short-lived cache so a preview call and the later registration share one model request.</summary>
public sealed class ItemDescriptionCache
{
    private readonly ConcurrentDictionary<string, (ItemDescription Value, DateTimeOffset Expires)> entries = new();

    public ItemDescription? Get(string key) =>
        entries.TryGetValue(key, out var entry) && entry.Expires > DateTimeOffset.UtcNow ? entry.Value : null;

    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> previews = new();

    /// <summary>Per-user allowance for photo previews so one account cannot spend the shared daily quota.</summary>
    public bool TryTakePreview(string user, int limit, TimeSpan window)
    {
        var list = previews.GetOrAdd(user, _ => []);
        lock (list)
        {
            var now = DateTimeOffset.UtcNow;
            list.RemoveAll(t => now - t > window);
            if (list.Count >= limit) return false;
            list.Add(now);
            return true;
        }
    }

    public void Set(string key, ItemDescription value)
    {
        if (entries.Count >= 200)
            foreach (var stale in entries.Where(e => e.Value.Expires <= DateTimeOffset.UtcNow).Select(e => e.Key).ToArray())
                entries.TryRemove(stale, out _);
        if (entries.Count < 200) entries[key] = (value, DateTimeOffset.UtcNow.AddHours(1));
    }
}

/// <summary>
/// Reads a found-item photo with the AI and stores the result as internal matching
/// text. The finder's own Description is never changed by this class.
/// </summary>
public sealed class ItemDescriber(CampusDb db, PrivateMedia media, AiProviders providers, ItemDescriptionCache cache)
{
    // Errors that retrying cannot fix; the item is indexed without AI text instead.
    private static readonly string[] Permanent =
        ["ai_not_configured", "ai_key_rejected", "ai_access_denied", "ai_request_rejected", "invalid_ai_response", "ai_images_not_permitted", "ai_quota_not_configured"];

    public async Task<ItemDescription> Describe(byte[] image, CancellationToken cancellation, bool fault = false)
    {
        var key = (fault ? "fault:" : "item:") + Convert.ToHexString(SHA256.HashData(CampusImageNormalizer.ToJpeg(image)));
        if (cache.Get(key) is { } cached) return cached;
        var description = fault ? await providers.DescribeFault(image, cancellation) : await providers.DescribeItem(image, cancellation);
        cache.Set(key, description);
        return description;
    }

    /// <summary>Suggestion shown to the finder while filling in the form.</summary>
    public async Task<object> Preview(string user, IFormFile photo, string? language, string? kind, CancellationToken cancellation)
    {
        if (photo.Length is <= 0 or > CampusImageNormalizer.MaximumUploadBytes) throw new RuleException("invalid_upload");
        if (!cache.TryTakePreview(user, 20, TimeSpan.FromHours(1))) throw new RuleException("rate_limited", 429);
        using var bytes = new MemoryStream();
        await photo.CopyToAsync(bytes, cancellation);
        var description = await Describe(bytes.ToArray(), cancellation, kind == "fault");
        return new { description = language == "en" ? description.DescriptionEn : description.DescriptionAr, category = description.Category };
    }

    /// <summary>Internal matching text: both languages plus colours and brand.</summary>
    public static string MatchingText(ItemDescription description)
    {
        var parts = new List<string> { description.DescriptionEn, description.DescriptionAr };
        if (description.Colors.Length > 0) parts.Add("colors: " + string.Join(", ", description.Colors));
        if (!string.IsNullOrEmpty(description.Brand)) parts.Add("brand: " + description.Brand);
        return string.Join(" | ", parts);
    }

    public async Task Process(BackgroundJob job, CancellationToken cancellation)
    {
        var item = await db.Items.FindAsync([job.EntityId], cancellation);
        if (item is null) return;
        var photo = await db.Media.Where(m => m.ItemId == item.Id).OrderBy(m => m.CreatedAt).FirstOrDefaultAsync(cancellation);
        if (photo is null) { GiveUp(item.Id, item); return; }
        try
        {
            var description = await Describe(await media.Read(photo), cancellation);
            item.AiDescription = MatchingText(description);
            item.AiCategory = description.Category;
            item.AiStatus = "ready";
            // Re-embed with the AI text (version 2 forces a refresh of the stored vector).
            await QueueIndex(item.Id, 2, cancellation);
        }
        // A spent quota or a rate limit must never hold the item (and, through "index pending",
        // every search) hostage: index it from the finder's text and skip the AI reading.
        catch (RuleException exception) when (Permanent.Contains(exception.Code) || job.Attempts >= 3 ||
            exception.Code is "ai_quota_exhausted" or "ai_rate_limited")
        {
            GiveUp(item.Id, item);
        }
    }

    /// <summary>Stop waiting for the AI: mark the item and make sure it still gets indexed.</summary>
    public void GiveUp(Guid itemId, FoundItem? item = null)
    {
        item ??= db.Items.Find(itemId);
        if (item is not null && item.AiStatus != "ready") item.AiStatus = "unavailable";
        var key = $"index:{itemId}:1";
        if (!db.Jobs.Local.Any(j => j.Key == key) && !db.Jobs.Any(j => j.Key == key))
            db.Jobs.Add(new() { Kind = "index", EntityId = itemId, InputVersion = 1, Key = key });
    }

    private async Task QueueIndex(Guid itemId, int version, CancellationToken cancellation)
    {
        var key = $"index:{itemId}:{version}";
        if (!await db.Jobs.AnyAsync(j => j.Key == key, cancellation))
            db.Jobs.Add(new() { Kind = "index", EntityId = itemId, InputVersion = version, Key = key });
    }
}
