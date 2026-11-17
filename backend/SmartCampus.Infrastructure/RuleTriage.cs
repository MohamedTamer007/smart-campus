namespace SmartCampus.Infrastructure;

/// <summary>
/// Keyword draft for a maintenance report, used only when no AI provider could answer.
/// It is a suggestion like the AI one: it never sets the staff decision, and its model name
/// ("rules-v1") and wording say it came from keywords.
/// </summary>
public static class RuleTriage
{
    public const string Model = "rules-v1";

    private static readonly (string Type, string[] Words)[] Rules =
    [
        ("safety", ["fire", "smoke", "gas", "hazard", "danger", "injury", "collapse", "حريق", "دخان", "غاز", "خطر", "اصابه", "انهيار"]),
        ("electrical", ["electric", "electrical", "socket", "outlet", "wire", "wiring", "spark", "short", "power", "كهرباء", "كهربا", "فيش", "سلك", "شرار", "ماس"]),
        ("water", ["leak", "leaking", "water", "flood", "تسريب", "مياه", "ميه", "مويه", "غرق"]),
        ("plumbing", ["pipe", "toilet", "sink", "drain", "faucet", "tap", "sewage", "سباكه", "ماسوره", "حنفيه", "مرحاض", "بيارة", "مجاري"]),
        ("hvac", ["ac", "conditioner", "conditioning", "heating", "ventilation", "تكييف", "مكيف", "تدفئه", "تهويه"]),
        ("lighting", ["light", "lights", "lamp", "bulb", "انارة", "انارت", "لمبه", "اضاءه", "نور"]),
        ("furniture", ["chair", "desk", "table", "bench", "board", "cabinet", "كرسي", "مكتب", "ترابيزه", "طاوله", "سبوره", "دولاب"]),
        ("doors", ["door", "lock", "handle", "hinge", "window", "باب", "قفل", "مقبض", "شباك", "نافذه"]),
    ];

    private static readonly string[] Urgent = ["urgent", "emergency", "immediately", "عاجل", "طارئ", "فورا", "خطر"];

    public static AiSuggestion Suggest(string description, string[] taxonomy, string[] priorities, string[] risks)
    {
        var tokens = SearchAttributes.RawTokens(description).ToHashSet(StringComparer.Ordinal);
        bool Has(string[] words) => words.Any(w => tokens.Contains(SearchRelevance.Normalize(w)));
        var type = Rules.FirstOrDefault(rule => Has(rule.Words) && taxonomy.Contains(rule.Type)).Type
            ?? (taxonomy.Contains("other") ? "other" : taxonomy[0]);
        var urgent = Has(Urgent);
        var level = type == "safety" || urgent ? "high" : type is "electrical" or "water" ? "medium" : "low";
        var priority = Pick(priorities, level);
        var risk = Pick(risks, type == "safety" ? "high" : type is "electrical" or "water" ? "medium" : "low");
        return new AiSuggestion(type, priority, risk,
            "مسودة مبنية على كلمات مفتاحية لأن خدمة الذكاء الاصطناعي لم تكن متاحة. يجب مراجعتها من الموظف.",
            "Keyword-based draft because no AI service was available. Staff must review it.")
        { Model = Model };
    }

    // Allowed values are configurable; pick the named level when present, else by position.
    private static string Pick(string[] values, string level)
    {
        if (values.Contains(level)) return level;
        return level == "high" ? values[^1] : level == "medium" ? values[values.Length / 2] : values[0];
    }
}
