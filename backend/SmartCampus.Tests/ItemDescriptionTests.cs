using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class ItemDescriptionTests
{
    private static string Envelope(string content, string finish = "stop") =>
        System.Text.Json.JsonSerializer.Serialize(new { model = "nvidia/nemotron-3.5-super-vl:free",
            choices = new[] { new { finish_reason = finish, message = new { content } } } });

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Parser_reads_both_languages_category_colors_and_brand()
    {
        var json = """{"description_en":"Silver wristwatch with a metal strap.","description_ar":"ساعة يد فضية بسوار معدني.","category":"Watch","colors":["silver","red","x","y"],"brand":"Casio"}""";
        var item = AiProviders.ParseItem(Envelope(json));
        Assert.Equal("watch", item.Category);
        Assert.Equal(["silver", "red", "x"], item.Colors);
        Assert.Equal("Casio", item.Brand);
        Assert.Contains("silver wristwatch", ItemDescriber.MatchingText(item), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ساعة يد", ItemDescriber.MatchingText(item));
        Assert.Contains("brand: Casio", ItemDescriber.MatchingText(item));
    }

    [Fact]
    public void Parser_accepts_fenced_json_and_maps_unknown_categories_to_other()
    {
        var fenced = "```json\n{\"description_en\":\"A bag\",\"description_ar\":\"شنطة\",\"category\":\"spaceship\"}\n```";
        Assert.Equal("other", AiProviders.ParseItem(Envelope(fenced)).Category);
    }

    [Theory]
    [InlineData("{\"description_en\":\"x\"}")]
    [InlineData("not json")]
    [InlineData("{\"description_en\":\"\",\"description_ar\":\"\"}")]
    public void Parser_rejects_incomplete_answers(string content) =>
        Assert.Equal("invalid_ai_response", Assert.Throws<RuleException>(() => AiProviders.ParseItem(Envelope(content))).Code);

    [Fact]
    public void Parser_rejects_a_cut_off_answer() =>
        Assert.Equal("invalid_ai_response", Assert.Throws<RuleException>(() => AiProviders.ParseItem(Envelope("{}", "length"))).Code);

    [Fact]
    public void Gate_needs_the_item_flag_provider_approval_key_and_quota()
    {
        var ok = new Dictionary<string, string?> {
            ["AI:AllowItemPhotoDescription"] = "true", ["AI:ProviderDataCollection"] = "allow", ["AI:OpenRouterKey"] = "synthetic" };
        Assert.True(AiProviders.CanDescribeItems(Config(ok)));
        foreach (var key in ok.Keys)
        {
            var broken = new Dictionary<string, string?>(ok) { [key] = key == "AI:OpenRouterKey" ? "" : "deny" };
            if (key == "AI:AllowItemPhotoDescription") broken[key] = "false";
            Assert.False(AiProviders.CanDescribeItems(Config(broken)), key);
        }
        Assert.False(AiProviders.CanDescribeItems(Config(new(ok) { ["AI:DescribeDailyRequestLimit"] = "0" })));
    }

    [Fact]
    public void Passage_adds_the_internal_text_but_keeps_the_finder_text()
    {
        var plain = new FoundItem { Description = "Silver watch" };
        Assert.Equal("Silver watch", RetrievalService.PassageText(plain));
        var enriched = new FoundItem { Description = "Silver watch", AiDescription = "Silver wristwatch | ساعة فضية", AiCategory = "watch" };
        Assert.Equal("Silver watch | Silver wristwatch | ساعة فضية | category: watch", RetrievalService.PassageText(enriched));
    }

    [Fact]
    public void Cache_returns_a_stored_description()
    {
        var cache = new ItemDescriptionCache();
        var description = new ItemDescription("ع", "e", "watch", [], null);
        Assert.Null(cache.Get("k"));
        cache.Set("k", description);
        Assert.Same(description, cache.Get("k"));
    }
}
