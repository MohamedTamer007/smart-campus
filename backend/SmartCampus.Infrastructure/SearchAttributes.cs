using System.Text;
using System.Text.RegularExpressions;

namespace SmartCampus.Infrastructure;

/// <summary>What a piece of text says about one object: brand, model number, colours.</summary>
public sealed record ItemAttributes(string? Brand, string? Model, string Variant, IReadOnlySet<string> Colors)
{
    public static readonly ItemAttributes Empty = new(null, null, "", new HashSet<string>());
}

/// <summary>
/// Cheap, model-free extraction of the details that separate "white iPhone 11" from
/// "dark red iPhone 17". It works on normalized Arabic/English text, needs no provider,
/// and is used both to re-rank embedding results and as the whole ranking when no
/// embedding provider is available.
/// </summary>
public static class SearchAttributes
{
    private static readonly Dictionary<string, string[]> ColorStems = new()
    {
        ["white"] = ["white", "ابيض", "بيضا"],
        ["black"] = ["black", "اسود", "سودا"],
        ["silver"] = ["silver", "فضي", "فضه"],
        ["gold"] = ["gold", "golden", "ذهبي", "دهبي"],
        ["blue"] = ["blue", "navy", "ازرق", "زرقا", "كحلي"],
        ["red"] = ["red", "maroon", "burgundy", "احمر", "حمرا", "بوردو"],
        ["green"] = ["green", "اخضر", "خضرا"],
        ["pink"] = ["pink", "وردي", "بينك"],
        ["purple"] = ["purple", "violet", "بنفسجي", "موف"],
        ["gray"] = ["gray", "grey", "رمادي"],
        ["brown"] = ["brown", "بني"],
        ["orange"] = ["orange", "برتقالي"],
        ["yellow"] = ["yellow", "اصفر", "صفرا"],
    };

    private static readonly Dictionary<string, string[]> Brands = new()
    {
        ["apple"] = ["iphone", "ايفون", "apple", "ابل", "airpods", "ايربودز", "macbook", "ipad", "ايباد", "ماك"],
        ["samsung"] = ["samsung", "سامسونج", "galaxy", "جالاكسي"],
        ["xiaomi"] = ["xiaomi", "redmi", "poco", "شاومي", "ريدمي"],
        ["huawei"] = ["huawei", "هواوي"],
        ["oppo"] = ["oppo", "اوبو"],
        ["realme"] = ["realme", "ريلمي"],
        ["honor"] = ["honor", "هونر"],
        ["casio"] = ["casio", "كاسيو"],
        ["rolex"] = ["rolex", "رولكس"],
        ["fossil"] = ["fossil", "فوسيل"],
        ["lenovo"] = ["lenovo", "لينوفو"],
        ["hp"] = ["hp"],
        ["dell"] = ["dell", "ديل"],
        ["asus"] = ["asus", "ايسوس"],
    };

    // Words after which a number is a model number ("iphone 11", "ايفون 11", "galaxy s23").
    private static readonly HashSet<string> ProductWords = new(StringComparer.Ordinal)
    {
        "iphone", "ايفون", "galaxy", "جالاكسي", "redmi", "ريدمي", "pixel", "note", "نوت", "ipad", "ايباد",
        "poco", "honor", "oppo", "realme", "xiaomi", "huawei", "samsung"
    };

    private static readonly HashSet<string> Variants = new(StringComparer.Ordinal)
    {
        "pro", "max", "plus", "mini", "ultra", "se", "برو", "ماكس", "بلس", "ميني", "الترا"
    };

    private static readonly Dictionary<string, string> BrandLookup = Build(Brands);
    private static readonly Dictionary<string, string> ColorLookup = Build(ColorStems);

    private static Dictionary<string, string> Build(Dictionary<string, string[]> source)
    {
        var lookup = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, words) in source)
            foreach (var word in words) lookup[SearchRelevance.Normalize(word)] = key;
        return lookup;
    }

    /// <summary>Normalized tokens with nothing dropped, so one-digit model numbers survive.</summary>
    public static string[] RawTokens(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        foreach (var c in SearchRelevance.Normalize(text))
        {
            if (char.IsLetterOrDigit(c)) current.Append(c);
            else if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); }
        }
        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens.ToArray();
    }

    public static ItemAttributes Extract(string text)
    {
        var tokens = RawTokens(text);
        string? brand = null, model = null;
        var variant = new List<string>();
        var colors = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < tokens.Length; i++)
        {
            var token = tokens[i];
            if (token.Length > 3 && token.StartsWith("ال", StringComparison.Ordinal)) token = token[2..];
            if (BrandLookup.TryGetValue(token, out var b)) brand ??= b;
            foreach (var (name, stems) in ColorStems)
                if (stems.Any(stem => { var s = SearchRelevance.Normalize(stem); return token == s || (s.Length >= 4 && token.StartsWith(s, StringComparison.Ordinal)); }))
                    colors.Add(name);
            if (model is null && ProductWords.Contains(token))
            {
                // The number may follow directly or after a variant word ("iphone pro 15").
                for (var j = i + 1; j < Math.Min(tokens.Length, i + 3); j++)
                {
                    if (tokens[j].All(char.IsDigit) && tokens[j].Length <= 3) { model = tokens[j]; break; }
                    if (!Variants.Contains(tokens[j]) && !(tokens[j].Length == 1 && char.IsLetter(tokens[j][0]))) break;
                }
                // A letter-number model such as "s23" or "a54".
                if (model is null && i + 1 < tokens.Length && Regex.IsMatch(tokens[i + 1], @"^[a-z]\d{1,3}$")) model = tokens[i + 1];
            }
            if (Variants.Contains(token)) variant.Add(token);
        }
        var variantKey = string.Join(",", variant.Select(NormalizeVariant).Distinct().OrderBy(x => x, StringComparer.Ordinal));
        return new ItemAttributes(brand, model, variantKey, colors);
    }

    private static string NormalizeVariant(string v) => v switch
    {
        "برو" => "pro", "ماكس" => "max", "بلس" => "plus", "ميني" => "mini", "الترا" => "ultra", _ => v
    };

    /// <summary>
    /// Score change from how well an item's details agree with the query. Agreement adds a
    /// little, a clear disagreement (a different model number or colour) removes a lot, and
    /// missing information changes nothing.
    /// </summary>
    public static double Adjustment(ItemAttributes query, ItemAttributes item)
    {
        var score = 0d;
        if (query.Brand is not null && item.Brand is not null)
            score += query.Brand == item.Brand ? 0.04 : -0.15;
        if (query.Model is not null && item.Model is not null)
        {
            if (query.Model != item.Model) score -= 0.20;
            else
            {
                score += 0.06;
                if (query.Variant != item.Variant && (query.Variant.Length > 0 || item.Variant.Length > 0)) score -= 0.06;
            }
        }
        if (query.Colors.Count > 0 && item.Colors.Count > 0)
            score += query.Colors.Overlaps(item.Colors) ? 0.03 : -0.08;
        return score;
    }

    private static readonly HashSet<string> GenericPlaceWords = new(StringComparer.Ordinal)
    {
        "كليه", "كلية", "college", "faculty", "building", "مبني", "مبنى", "قاعه", "hall", "room", "الدور", "دور", "floor",
        "university", "جامعه", "campus"
    };

    /// <summary>Small boost when the query names the place the item was found in.</summary>
    public static double PlaceBoost(string queryText, string place)
    {
        if (string.IsNullOrWhiteSpace(place)) return 0;
        var query = SearchRelevance.Tokens(queryText);
        var hits = SearchRelevance.Tokens(place)
            .Where(p => p.Length >= 3 && !GenericPlaceWords.Contains(p))
            .Count(p => query.Any(q => q == p || (q.Length >= 4 && p.Length >= 4 && (q.StartsWith(p, StringComparison.Ordinal) || p.StartsWith(q, StringComparison.Ordinal)))));
        return hits >= 2 ? 0.05 : hits == 1 ? 0.04 : 0;
    }

    private static readonly Regex DatePattern = new(@"(?<!\d)(\d{1,2})\s*(?:شهر|/|-|\.|month)\s*(\d{1,2})(?!\d)", RegexOptions.Compiled);

    /// <summary>Small boost when a day/month written in the query matches the date the item was found.</summary>
    public static double DateBoost(string queryText, DateOnly? date)
    {
        if (date is null) return 0;
        var match = DatePattern.Match(SearchRelevance.Normalize(queryText));
        if (!match.Success) return 0;
        var first = int.Parse(match.Groups[1].Value);
        var second = int.Parse(match.Groups[2].Value);
        return (first == date.Value.Day && second == date.Value.Month) || (first == date.Value.Month && second == date.Value.Day) ? 0.03 : 0;
    }
}
