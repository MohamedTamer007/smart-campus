using Xunit;
using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using SmartCampus.Domain;
using SmartCampus.Infrastructure;

namespace SmartCampus.Tests;

public sealed class FallbackAndRankingTests
{
    private const string Query = "ايفون 11 لونه ابيض ضاع مني في كلية سياحة يوم 16 شهر 11";

    private static PooledCandidate Item(double score, string text, string? category = "phone", string place = "", DateOnly? date = null) =>
        new(new RetrievalCandidate(Guid.NewGuid(), score, score, null), text, category, place, date);

    [Fact]
    public void Same_model_and_colour_rises_and_a_different_model_and_colour_is_dropped()
    {
        // The reported case: the right white iPhone 11 scored 67% and an iPhone 17 59.8%.
        var right = Item(0.673, "ايفون 11 ابيضاني", place: "كلية سياحة وفنادق الدور الرابع", date: new DateOnly(2026, 11, 16));
        var wrong = Item(0.598, "ايفون 17 dark red iPhone 17", place: "صيدلة", date: new DateOnly(2026, 11, 14));
        var result = SearchRelevance.Select(Query, [right, wrong], new RelevanceOptions());
        var only = Assert.Single(result);
        Assert.Equal(right.Candidate.ItemId, only.Candidate.ItemId);
        Assert.True(only.Candidate.Score >= 0.70, "a clearly matching item must reach the Medium band");
        Assert.Equal(0.673, only.Candidate.TextScore); // the raw similarity is kept separately
    }

    [Fact]
    public void A_lone_conflicting_item_is_still_listed_but_ranked_low()
    {
        var wrong = Item(0.60, "ايفون 17 dark red");
        var only = Assert.Single(SearchRelevance.Select(Query, [wrong], new RelevanceOptions()));
        Assert.True(only.Candidate.Score < 0.5);
    }

    [Theory]
    [InlineData("iphone 11 pro white", "apple", "11", "pro", "white")]
    [InlineData("ايفون ١١ ابيض", "apple", "11", "", "white")]
    [InlineData("samsung galaxy s23 black", "samsung", "s23", "", "black")]
    [InlineData("silver watch", null, null, "", "silver")]
    public void Attributes_are_read_from_arabic_and_english_text(string text, string? brand, string? model, string variant, string colour)
    {
        var attributes = SearchAttributes.Extract(text);
        Assert.Equal(brand, attributes.Brand);
        Assert.Equal(model, attributes.Model);
        Assert.Equal(variant, attributes.Variant);
        Assert.Contains(colour, attributes.Colors);
    }

    [Fact]
    public void The_date_in_a_query_is_not_mistaken_for_a_model_number()
    {
        Assert.Equal("11", SearchAttributes.Extract(Query).Model);
        Assert.Equal(0.03, SearchAttributes.DateBoost(Query, new DateOnly(2026, 11, 16)));
        Assert.Equal(0, SearchAttributes.DateBoost(Query, new DateOnly(2026, 11, 3)));
        Assert.True(SearchAttributes.PlaceBoost(Query, "كلية سياحة وفنادق") > 0);
        Assert.Equal(0, SearchAttributes.PlaceBoost(Query, "كلية صيدلة"));
    }

    [Fact]
    public void Keyword_ranking_needs_no_embedding_and_puts_the_right_item_first()
    {
        var items = new[]
        {
            Item(0, "ايفون 17 dark red", place: "صيدلة"),
            Item(0, "ايفون 11 ابيضاني", place: "كلية سياحة وفنادق", date: new DateOnly(2026, 11, 16)),
            Item(0, "محفظة جلد بني", category: "wallet"),
        };
        var ranked = SearchRelevance.KeywordRank(Query, items, new RelevanceOptions());
        Assert.Equal(items[1].Candidate.ItemId, ranked[0].Candidate.ItemId);
        Assert.DoesNotContain(ranked, r => r.Candidate.ItemId == items[2].Candidate.ItemId);
        Assert.All(ranked, r => Assert.Null(r.Candidate.TextScore));
    }

    [Fact]
    public void Rule_triage_gives_a_labelled_keyword_draft_inside_the_allowed_values()
    {
        string[] types = ["plumbing", "water", "electrical", "hvac", "lighting", "furniture", "doors", "safety", "other"];
        string[] levels = ["low", "medium", "high"];
        var leak = RuleTriage.Suggest("Water leak from the ceiling in lab 3", types, levels, levels);
        Assert.Equal("water", leak.Type);
        Assert.Equal(RuleTriage.Model, leak.Model);
        var fire = RuleTriage.Suggest("smoke from the socket, urgent", types, levels, levels);
        Assert.Equal("safety", fire.Type); // smoke is a safety signal and outranks the socket
        Assert.Equal("high", fire.Priority);
        Assert.Equal("high", fire.Risk);
        Assert.Equal("electrical", RuleTriage.Suggest("the socket near the door is not working", types, levels, levels).Type);
        Assert.Equal("other", RuleTriage.Suggest("something odd", types, levels, levels).Type);
    }

    // ---- provider chain ----

    private sealed class Route(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler, IHttpClientFactory
    {
        public List<string> Calls { get; } = [];
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls.Add(request.RequestUri!.Host + (request.Headers.TryGetValues("x-goog-api-key", out var k) ? "#" + k.First() : ""));
            return Task.FromResult(respond(request));
        }
    }

    private sealed class OpenBudget : IInferenceBudget
    {
        public Task Reserve(string provider, CancellationToken cancellation) => Task.CompletedTask;
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private static string OpenRouterOk(string text) =>
        System.Text.Json.JsonSerializer.Serialize(new { model = LlmChain.OpenRouterModel, choices = new[] { new { finish_reason = "stop", message = new { content = text } } } });
    private static string GeminiOk(string text) =>
        System.Text.Json.JsonSerializer.Serialize(new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } });

    private static IConfiguration Config(bool cross, params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string?>
        {
            ["AI:Provider"] = "openrouter", ["AI:ProviderDataCollection"] = "allow", ["AI:OpenRouterKey"] = "or-primary",
            ["AI:AllowCrossProviderFallback"] = cross ? "true" : "false", ["AI:GeminiFreeTierOnly"] = "true", ["AI:GeminiKey"] = "gem-primary",
        };
        foreach (var (key, value) in extra) values[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static readonly LlmRequest Request = new("system", "user", null, 100);

    [Fact]
    public async Task Gemini_answers_when_openrouter_is_out_of_quota()
    {
        var route = new Route(r => r.RequestUri!.Host.Contains("openrouter") ? new HttpResponseMessage(HttpStatusCode.TooManyRequests) : Json(GeminiOk("{\"ok\":true}")));
        var reply = await new LlmChain(route, Config(true), new OpenBudget(), new LlmHealth()).Complete(Request, "maintenance", default);
        Assert.Equal("gemini", reply.Backend);
        Assert.Equal("{\"ok\":true}", reply.Text);
        Assert.Contains(route.Calls, c => c.StartsWith("openrouter.ai"));
    }

    [Fact]
    public async Task OpenRouter_answers_when_gemini_is_first_in_order_and_down()
    {
        var route = new Route(r => r.RequestUri!.Host.Contains("googleapis") ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Json(OpenRouterOk("{\"ok\":1}")));
        var reply = await new LlmChain(route, Config(true, ("AI:FallbackOrder", "gemini,openrouter")), new OpenBudget(), new LlmHealth()).Complete(Request, "describe", default);
        Assert.Equal("openrouter", reply.Backend);
    }

    [Fact]
    public async Task A_backup_key_is_tried_before_changing_provider()
    {
        var route = new Route(r => r.Headers.Authorization?.Parameter == "or-primary" ? new HttpResponseMessage(HttpStatusCode.Unauthorized) : Json(OpenRouterOk("{\"ok\":2}")));
        var reply = await new LlmChain(route, Config(true, ("AI:OpenRouterKeyBackup", "or-backup")), new OpenBudget(), new LlmHealth()).Complete(Request, "maintenance", default);
        Assert.Equal("openrouter", reply.Backend);
        Assert.Equal(2, route.Calls.Count);
        Assert.DoesNotContain(route.Calls, c => c.StartsWith("generativelanguage"));
    }

    [Fact]
    public async Task Without_the_owner_flag_Gemini_is_never_called_and_the_original_error_is_kept()
    {
        var route = new Route(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var failure = await Assert.ThrowsAsync<RuleException>(() => new LlmChain(route, Config(false), new OpenBudget(), new LlmHealth()).Complete(Request, "maintenance", default));
        Assert.Equal("ai_rate_limited", failure.Code);
        Assert.DoesNotContain(route.Calls, c => c.StartsWith("generativelanguage"));
    }

    [Fact]
    public async Task When_every_provider_fails_the_last_error_is_returned_and_a_failing_provider_is_skipped_next_time()
    {
        var route = new Route(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var chain = new LlmChain(route, Config(true), new OpenBudget(), new LlmHealth());
        var failure = await Assert.ThrowsAsync<RuleException>(() => chain.Complete(Request, "maintenance", default));
        Assert.Equal("ai_unavailable", failure.Code);
        var calls = route.Calls.Count;
        await Assert.ThrowsAsync<RuleException>(() => chain.Complete(Request, "maintenance", default));
        Assert.Equal(calls, route.Calls.Count); // both providers are cooling down: no new requests
    }

    [Fact]
    public async Task Nothing_configured_reports_ai_not_configured()
    {
        var empty = new ConfigurationBuilder().Build();
        var failure = await Assert.ThrowsAsync<RuleException>(() => new LlmChain(new Route(_ => Json("{}")), empty, new OpenBudget(), new LlmHealth()).Complete(Request, "maintenance", default));
        Assert.Equal("ai_not_configured", failure.Code);
    }

    [Fact]
    public void Gates_need_the_owner_flag_a_key_and_the_free_tier_attestation()
    {
        Assert.True(LlmChain.GeminiReady(Config(true)));
        Assert.False(LlmChain.GeminiReady(Config(false)));
        Assert.False(LlmChain.GeminiReady(Config(true, ("AI:GeminiFreeTierOnly", "false"))));
        Assert.False(LlmChain.GeminiReady(Config(true, ("AI:GeminiKey", ""))));
        Assert.True(LlmChain.GeminiReady(Config(true, ("AI:GeminiKey", ""), ("AI:GeminiKeyBackup", "gem-backup"))));
    }
}
