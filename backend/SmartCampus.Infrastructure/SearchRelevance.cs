using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace SmartCampus.Infrastructure;

public sealed record PooledCandidate(RetrievalCandidate Candidate, string Text, string? AiCategory, string Place = "", DateOnly? Date = null);

public sealed record RelevanceOptions(bool CategoryGate = true, double Margin = 0.08, double KeywordWeight = 0.1, int Take = 5);

/// <summary>
/// Re-ranks the nearest-vector pool so a "silver watch" search does not list phones.
/// It adds three cheap, explainable signals to the embedding score: a category check
/// (rules, no model call), keyword overlap on normalized Arabic/English text, and a
/// gap to the best result. None of them is a calibrated probability.
/// </summary>
public static class SearchRelevance
{
    private static readonly HashSet<string> Stop = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "of", "in", "on", "with", "for", "and", "or", "my", "i", "is", "it", "to", "at", "from", "lost", "found",
        "من", "في", "و", "علي", "عن", "نوعها", "نوع", "لون", "جوه", "جوة", "فقدت", "ضاع", "ضاعت", "لقيت"
    };

    // Lexicon of normalized words per category. Words are matched as whole tokens.
    private static readonly Dictionary<string, string[]> Lexicon = new()
    {
        ["watch"] = ["watch", "watches", "smartwatch", "wristwatch", "ساعه", "ساعات", "رولكس", "كاسيو"],
        ["phone"] = ["phone", "phones", "iphone", "smartphone", "cellphone", "mobile", "android", "ايفون", "موبايل", "تليفون", "هاتف", "جوال", "محمول"],
        ["wallet"] = ["wallet", "purse", "محفظه"],
        ["keys"] = ["key", "keys", "keychain", "مفتاح", "مفاتيح"],
        ["bag"] = ["bag", "backpack", "handbag", "شنطه", "حقيبه"],
        ["id_card"] = ["id", "card", "كارنيه", "بطاقه", "هويه"],
        ["headphones"] = ["headphones", "earphones", "earbuds", "airpods", "headset", "سماعه", "سماعات", "ايربودز"],
        ["laptop"] = ["laptop", "macbook", "لابتوب", "حاسوب", "كمبيوتر"],
        ["bottle"] = ["bottle", "flask", "thermos", "زجاجه", "قزازه", "ترمس"],
        ["clothing"] = ["jacket", "coat", "hoodie", "shirt", "sweater", "scarf", "cap", "جاكيت", "سترة", "بلوفر", "قميص", "تيشرت", "وشاح", "قبعه"],
        ["glasses"] = ["glasses", "sunglasses", "eyeglasses", "نظاره", "نضاره"],
        ["jewelry"] = ["ring", "necklace", "bracelet", "earring", "jewelry", "خاتم", "دبله", "قلاده", "سوار", "اسواره", "حلق"],
    };

    private static readonly Dictionary<string, string> WordToCategory = BuildLookup();

    private static Dictionary<string, string> BuildLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (category, words) in Lexicon)
            foreach (var word in words) lookup[Normalize(word)] = category;
        return lookup;
    }

    /// <summary>Lower-case, Arabic letter forms unified, diacritics/tatweel removed, Arabic-Indic digits to Latin.</summary>
    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var raw in text.ToLowerInvariant())
        {
            var c = raw;
            if (c is >= 'ً' and <= 'ٟ' or 'ـ') continue;
            if (c is >= '٠' and <= '٩') c = (char)('0' + (c - '٠'));
            else if (c is >= '۰' and <= '۹') c = (char)('0' + (c - '۰'));
            else c = c switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ة' => 'ه',
                'ى' => 'ي',
                'ؤ' => 'و',
                'ئ' => 'ي',
                _ => c
            };
            builder.Append(c);
        }
        return builder.ToString();
    }

    public static string[] Tokens(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        void Flush()
        {
            if (current.Length == 0) return;
            var token = current.ToString();
            current.Clear();
            if (token.Length > 3 && token.StartsWith("ال", StringComparison.Ordinal)) token = token[2..];
            if (token.Length >= 2 && !Stop.Contains(token)) tokens.Add(token);
        }
        foreach (var c in Normalize(text))
        {
            if (char.IsLetterOrDigit(c)) current.Append(c);
            else Flush();
        }
        Flush();
        return tokens.ToArray();
    }

    /// <summary>The single category a text clearly names, or null when it names none or several.</summary>
    public static string? CategoryOf(string text)
    {
        var found = Tokens(text).Select(t => WordToCategory.GetValueOrDefault(t)).Where(c => c is not null).Distinct().ToArray();
        return found.Length == 1 ? found[0] : null;
    }

    /// <summary>Share of query words found in the item text (exact, or one a prefix of the other).</summary>
    public static double Overlap(string[] query, string[] item)
    {
        if (query.Length == 0) return 0;
        var hits = query.Count(q => item.Any(i => i == q || (q.Length >= 4 && i.Length >= 4 && (i.StartsWith(q, StringComparison.Ordinal) || q.StartsWith(i, StringComparison.Ordinal)))));
        return (double)hits / query.Length;
    }

    public static PooledCandidate[] Select(string query, IReadOnlyList<PooledCandidate> pool, RelevanceOptions options)
    {
        if (pool.Count == 0) return [];
        var queryTokens = Tokens(query);
        var queryAttributes = SearchAttributes.Extract(query);
        var wanted = options.CategoryGate ? CategoryOf(query) : null;
        var kept = pool.Where(p =>
        {
            if (wanted is null) return true;
            // An item whose category is unknown is never dropped: recall matters more than tidiness.
            var category = string.IsNullOrEmpty(p.AiCategory) || p.AiCategory == "other" ? CategoryOf(p.Text) : p.AiCategory;
            return category is null || category == wanted;
        }).ToArray();
        if (kept.Length == 0) return [];
        // Details (model, colour, brand) and the place/date the query mentions move the score:
        // up when they agree, sharply down when they clearly disagree.
        var adjusted = kept.Select(p =>
        {
            var delta = SearchAttributes.Adjustment(queryAttributes, SearchAttributes.Extract(p.Text))
                + SearchAttributes.PlaceBoost(query, p.Place) + SearchAttributes.DateBoost(query, p.Date);
            var score = Math.Clamp(p.Candidate.Score + delta, 0, 1);
            return p with { Candidate = p.Candidate with { Score = score } };
        }).ToArray();
        var best = adjusted.Max(p => p.Candidate.Score);
        return adjusted
            .Where(p => best - p.Candidate.Score <= options.Margin)
            .OrderByDescending(p => p.Candidate.Score + options.KeywordWeight * Overlap(queryTokens, Tokens(p.Text)))
            .ThenBy(p => p.Candidate.ItemId)
            .Take(options.Take)
            .ToArray();
    }

    /// <summary>
    /// Ranking with no embedding at all: how many of the query's words the item text contains,
    /// then the same detail/place/date signals. Used when no embedding provider is available.
    /// Items sharing no word and no category with the query are not returned.
    /// </summary>
    public static PooledCandidate[] KeywordRank(string query, IReadOnlyList<PooledCandidate> items, RelevanceOptions options)
    {
        var queryTokens = Tokens(query);
        var wanted = CategoryOf(query);
        var scored = new List<PooledCandidate>();
        foreach (var item in items)
        {
            var overlap = Overlap(queryTokens, Tokens(item.Text + " " + item.Place));
            var category = string.IsNullOrEmpty(item.AiCategory) || item.AiCategory == "other" ? CategoryOf(item.Text) : item.AiCategory;
            var sameCategory = wanted is not null && category == wanted;
            if (overlap <= 0 && !sameCategory) continue;
            scored.Add(item with { Candidate = item.Candidate with { Score = 0.35 + 0.45 * overlap, TextScore = null, ImageScore = null } });
        }
        return Select(query, scored, options);
    }

    public static RelevanceOptions OptionsFrom(IConfiguration config) => new(
        config.GetValue("Search:CategoryGate", true),
        Math.Clamp(config.GetValue("Search:RelevanceMargin", 0.08), 0, 1),
        Math.Clamp(config.GetValue("Search:KeywordWeight", 0.1), 0, 1));
}
