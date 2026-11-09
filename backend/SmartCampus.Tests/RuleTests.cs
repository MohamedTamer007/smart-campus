using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class RuleTests
{
    [Theory]
    [InlineData("a@o6u.edu.eg.evil.com")]
    [InlineData("a@sub.o6u.edu.eg")]
    [InlineData("Name <a@o6u.edu.eg>")]
    [InlineData("a@@o6u.edu.eg")]
    [InlineData("a b@o6u.edu.eg")]
    public void Deceptive_email_is_rejected(string email) => Assert.Throws<RuleException>(() => Rules.UniversityEmail(email));

    [Fact] public void University_email_is_normalized() => Assert.Equal("student@o6u.edu.eg", Rules.UniversityEmail(" STUDENT@O6U.EDU.EG "));

    [Theory]
    [InlineData("submitted", "resolved")]
    [InlineData("submitted", "rejected")]
    [InlineData("repair", "rejected")]
    [InlineData("resolved", "review")]
    [InlineData("rejected", "submitted")]
    public void Invalid_maintenance_transition_is_rejected(string before, string after) => Assert.Throws<RuleException>(() => Rules.Transition(before, after));

    [Fact]
    public void Unrecognized_or_truncated_AI_output_is_rejected()
    {
        Assert.Throws<RuleException>(() => AiProviders.Parse("{}", ["plumbing"], ["normal"], ["low"]));
        var envelope = """{"choices":[{"finish_reason":"length","message":{"content":"{}"}}]}""";
        Assert.Throws<RuleException>(() => AiProviders.Parse(envelope, ["plumbing"], ["normal"], ["low"]));
        envelope = """{"choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"invented\",\"priority\":\"normal\",\"risk\":\"low\",\"explanation\":\"Evidence\"}"}}]}""";
        Assert.Throws<RuleException>(() => AiProviders.Parse(envelope, ["plumbing"], ["normal"], ["low"]));
    }

    [Fact]
    public void Valid_AI_output_preserves_priority_and_risk_separately()
    {
        var envelope = """{"choices":[{"finish_reason":"stop","message":{"content":"{\"type\":\"plumbing\",\"priority\":\"urgent\",\"risk\":\"high\",\"explanation\":\"Water near electricity\"}"}}]}""";
        var suggestion = AiProviders.Parse(envelope, ["plumbing"], ["urgent"], ["high"]);
        Assert.Equal("urgent", suggestion.Priority);
        Assert.Equal("high", suggestion.Risk);
    }

    [Fact]
    public void Embedding_revision_dimension_and_normalization_are_enforced()
    {
        RetrievalService.Validate(new(RetrievalService.Model, RetrievalService.Revision, 2, [1, 0]));
        Assert.Throws<RuleException>(() => RetrievalService.Validate(new(RetrievalService.Model, "wrong", 2, [1, 0])));
        Assert.Throws<RuleException>(() => RetrievalService.Validate(new(RetrievalService.Model, RetrievalService.Revision, 3, [1, 0])));
        Assert.Throws<RuleException>(() => RetrievalService.Validate(new(RetrievalService.Model, RetrievalService.Revision, 2, [float.NaN, 0])));
        Assert.Throws<RuleException>(() => RetrievalService.Validate(new(RetrievalService.Model, RetrievalService.Revision, 2, [0, 0])));
    }
}
