using SmartCampus.Domain;
using SmartCampus.Infrastructure;
using Microsoft.Extensions.Configuration;
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

    [Fact]
    public void Optional_text_accepts_empty_and_boundary_values_but_rejects_oversized_content()
    {
        Assert.Equal("", Rules.OptionalText(null));
        Assert.Equal("", Rules.OptionalText("   "));
        Assert.Equal(new string('x', 4000), Rules.OptionalText(new string('x', 4000)));
        Assert.Throws<RuleException>(() => Rules.OptionalText(new string('x', 4001)));
    }

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

    [Fact]
    public void Hosted_search_requires_independently_approved_thresholds_for_all_three_query_modes()
    {
        var values = new Dictionary<string, string?>
        {
            ["Search:Provider"] = "hosted",
            ["Search:CalibrationApproval"] = "reviewed synthetic calibration",
            ["Search:CalibrationRevision"] = "gemini-embedding-2-google-768-v1",
            ["Search:MinimumScore"] = "0.9"
        };
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.Null(RetrievalService.ApprovedThreshold(config));
        Assert.Null(RetrievalService.ApprovedThreshold(config, hasImage: true));
        Assert.False(RetrievalService.CalibrationApproved(config));

        values["Search:TextMinimumScore"] = "0.42";
        config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Equal(0.42, RetrievalService.ApprovedThreshold(config, hasImage: false));
        Assert.Null(RetrievalService.ApprovedThreshold(config, hasImage: true));
        Assert.False(RetrievalService.CalibrationApproved(config));

        values["Search:MultimodalMinimumScore"] = "0.91";
        config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Equal(0.42, RetrievalService.ApprovedThreshold(config, hasImage: false));
        Assert.Equal(0.91, RetrievalService.ApprovedThreshold(config, hasImage: true));
        Assert.Null(RetrievalService.ApprovedThreshold(config, hasImage: true, hasText: false));
        Assert.False(RetrievalService.CalibrationApproved(config));

        values["Search:ImageMinimumScore"] = "0.87";
        config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.Equal(0.87, RetrievalService.ApprovedThreshold(config, hasImage: true, hasText: false));
        Assert.True(RetrievalService.CalibrationApproved(config));

        values["Search:CalibrationRevision"] = "stale-model-revision";
        config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.False(RetrievalService.CalibrationApproved(config));
        Assert.Null(RetrievalService.ApprovedThreshold(config, hasImage: false));

        values["Search:CalibrationRevision"] = "nemotron35-8b-openrouter-1024-v1";
        config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.False(RetrievalService.CalibrationApproved(config));
        Assert.Null(RetrievalService.ApprovedThreshold(config, hasImage: false));
    }

    [Theory]
    [InlineData("NaN", false)]
    [InlineData("Infinity", false)]
    [InlineData("1.01", false)]
    [InlineData("-1.01", false)]
    [InlineData("NaN", true)]
    [InlineData("Infinity", true)]
    [InlineData("1.01", true)]
    [InlineData("-1.01", true)]
    public void Hosted_search_rejects_non_finite_or_out_of_range_modality_thresholds(string threshold, bool hasImage)
    {
        IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Search:Provider"] = "hosted",
            ["Search:CalibrationApproval"] = "reviewed",
            ["Search:CalibrationRevision"] = "gemini-embedding-2-google-768-v1",
            ["Search:TextMinimumScore"] = hasImage ? "0.8" : threshold,
            ["Search:MultimodalMinimumScore"] = hasImage ? threshold : "0.8"
        }).Build();

        Assert.Null(RetrievalService.ApprovedThreshold(config, hasImage));
        Assert.False(RetrievalService.CalibrationApproved(config));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1.01")]
    [InlineData("-1.01")]
    public void Hosted_image_only_search_rejects_invalid_calibration_thresholds(string threshold)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Search:Provider"] = "hosted", ["Search:CalibrationApproval"] = "test only",
            ["Search:CalibrationRevision"] = "gemini-embedding-2-google-768-v1",
            ["Search:TextMinimumScore"] = "0.8", ["Search:MultimodalMinimumScore"] = "0.8",
            ["Search:ImageMinimumScore"] = threshold
        }).Build();
        Assert.Null(RetrievalService.ApprovedThreshold(config, hasImage: true, hasText: false));
        Assert.False(RetrievalService.CalibrationApproved(config));
    }
}
