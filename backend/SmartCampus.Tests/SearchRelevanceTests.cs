using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class SearchRelevanceTests
{
    private static PooledCandidate Item(string text, double score, string? category = null) =>
        new(new RetrievalCandidate(Guid.NewGuid(), score, score, null), text, category);

    private static readonly RelevanceOptions Defaults = new();

    [Fact]
    public void Arabic_forms_digits_and_the_article_are_normalized()
    {
        Assert.Equal("ساعه", SearchRelevance.Normalize("ساعة"));
        Assert.Equal("غرفه 204", SearchRelevance.Normalize("غرفة ٢٠٤"));
        Assert.Equal(["ساعه", "فضي"], SearchRelevance.Tokens("الساعة الفضي"));
        Assert.Equal(["silver", "watch"], SearchRelevance.Tokens("The Silver WATCH"));
    }

    [Fact]
    public void Category_is_named_only_when_exactly_one_is_clear()
    {
        Assert.Equal("watch", SearchRelevance.CategoryOf("ساعه فضي"));
        Assert.Equal("phone", SearchRelevance.CategoryOf("ايفون ١١ ابيضاني"));
        Assert.Null(SearchRelevance.CategoryOf("silver thing"));
        Assert.Null(SearchRelevance.CategoryOf("watch and phone"));
    }

    [Fact]
    public void Silver_watch_search_drops_the_phones_from_the_reported_screenshot()
    {
        var casio = Item("ساعة فضي من جوة احمر نوعها كاسيو", 0.667);
        var rolex = Item("ساعة فضي من جوة اخضر نوعها رولكس", 0.653);
        var iphone11 = Item("ايفون 11 ابيضاني", 0.573);
        var iphone17 = Item("ايفون 17", 0.541);
        var result = SearchRelevance.Select("ساعه فضي", [iphone17, casio, iphone11, rolex], Defaults);
        Assert.Equal([casio.Candidate.ItemId, rolex.Candidate.ItemId], result.Select(r => r.Candidate.ItemId).ToArray());
    }

    [Fact]
    public void Category_gate_alone_removes_a_phone_even_when_its_score_is_close()
    {
        var watch = Item("silver watch", 0.70);
        var phone = Item("white iphone", 0.69);
        var result = SearchRelevance.Select("silver watch", [phone, watch], Defaults with { Margin = 1 });
        Assert.Equal(watch.Candidate.ItemId, Assert.Single(result).Candidate.ItemId);
    }

    [Fact]
    public void Items_with_unknown_category_are_never_dropped_by_the_gate()
    {
        var unknown = Item("small silver object", 0.65);
        var other = Item("thing", 0.64, "other");
        var result = SearchRelevance.Select("silver watch", [unknown, other], Defaults);
        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void Stored_ai_category_overrides_wording()
    {
        var phone = Item("small device", 0.70, "phone");
        var watch = Item("small device", 0.69, "watch");
        var result = SearchRelevance.Select("watch", [phone, watch], Defaults);
        Assert.Equal(watch.Candidate.ItemId, Assert.Single(result).Candidate.ItemId);
    }

    [Fact]
    public void A_lone_weak_match_is_still_returned()
    {
        var only = Item("anything", 0.40);
        Assert.Single(SearchRelevance.Select("watch", [only], Defaults));
    }

    [Fact]
    public void Keyword_overlap_orders_close_scores_and_gap_hides_distant_ones()
    {
        var exact = Item("silver watch casio", 0.62);
        var vague = Item("metal object", 0.64);
        var far = Item("metal object", 0.40);
        var result = SearchRelevance.Select("silver watch", [vague, far, exact], Defaults);
        Assert.Equal([exact.Candidate.ItemId, vague.Candidate.ItemId], result.Select(r => r.Candidate.ItemId).ToArray());
    }

    [Fact]
    public void Gate_can_be_switched_off_and_the_result_is_capped_at_five()
    {
        var pool = Enumerable.Range(0, 8).Select(i => Item("silver watch " + i, 0.7 - i * 0.001)).ToArray();
        Assert.Equal(5, SearchRelevance.Select("silver watch", pool, Defaults).Length);
        var phone = Item("iphone", 0.7);
        Assert.Single(SearchRelevance.Select("watch", [phone], Defaults with { CategoryGate = false }));
    }
}
