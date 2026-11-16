namespace SmartCampus.Infrastructure;

internal static class AssessmentExplanationText
{
    private const string Marker = "[[smart-campus-bilingual-v1]]\n";
    private const string EnglishSeparator = "\n[[english-v1]]\n";
    private const int MaxLanguageLength = 1900;

    public static string Encode(string arabic, string english)
    {
        static string Clean(string value) => new(value.Where(c => !char.IsControl(c)).Take(MaxLanguageLength).ToArray());
        return Marker + Clean(arabic) + EnglishSeparator + Clean(english);
    }

    public static (string? Arabic, string? English) Decode(string? stored)
    {
        if (stored is null) return (null, null);
        if (!stored.StartsWith(Marker, StringComparison.Ordinal)) return (null, stored);
        var separator = stored.IndexOf(EnglishSeparator, Marker.Length, StringComparison.Ordinal);
        if (separator < 0) return (null, stored);
        return (stored[Marker.Length..separator], stored[(separator + EnglishSeparator.Length)..]);
    }
}
