using SmartCampus.Infrastructure;
using Xunit;

namespace SmartCampus.Tests;

public sealed class RecurrenceAnalysisTests
{
    private static readonly Guid Building = Guid.NewGuid(), Floor = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyDictionary<Guid, float[]> NoVectors = new Dictionary<Guid, float[]>();

    private static RecurrenceReport Report(string details, int daysAgo, int? resolvedDaysAgo = null, string type = "doors", string description = "problem") =>
        new(Guid.NewGuid(), type, Building, Floor, description, details, Now.AddDays(-daysAgo), resolvedDaysAgo is null ? null : Now.AddDays(-resolvedDaysAgo.Value));

    [Theory]
    [InlineData("باب غرفة ٢٠٤ مكسور", "room204")]
    [InlineData("Door of room 204", "room204")]
    [InlineData("lab 3 projector", "lab3")]
    [InlineData("معمل 3", "lab3")]
    [InlineData("the corridor door", null)]
    public void Room_and_lab_numbers_become_one_key_in_both_languages(string text, string? key) =>
        Assert.Equal(key, RecurrenceAnalysis.LocationKey(text));

    [Fact]
    public void Same_room_groups_together_and_a_different_room_does_not()
    {
        var reports = new[] { Report("room 204", 30), Report("غرفة ٢٠٤", 20), Report("room 205", 10) };
        var group = Assert.Single(RecurrenceAnalysis.Analyze(reports, NoVectors, 0.85, Now));
        Assert.Equal("room204", group.Location);
        Assert.Equal(2, group.ReportIds.Length);
    }

    [Fact]
    public void Without_embeddings_only_equal_wording_joins_unnumbered_reports()
    {
        var same = new[] { Report("corridor door", 9), Report("Corridor Door", 5) };
        Assert.Single(RecurrenceAnalysis.Analyze(same, NoVectors, 0.85, Now));
        var different = new[] { Report("corridor door", 9), Report("stairs door", 5) };
        Assert.Empty(RecurrenceAnalysis.Analyze(different, NoVectors, 0.85, Now));
    }

    [Fact]
    public void Similar_embeddings_join_reports_worded_differently_but_never_different_rooms()
    {
        var a = Report("lab 3 door broken", 9);
        var b = Report("the third lab door will not close", 5);
        var c = Report("lab 4 door broken", 3);
        var vectors = new Dictionary<Guid, float[]> { [a.Id] = [1, 0], [b.Id] = [0.99f, 0.1f], [c.Id] = [1, 0] };
        var groups = RecurrenceAnalysis.Analyze([a, b, c], vectors, 0.85, Now);
        var group = Assert.Single(groups);
        Assert.Equal(new[] { a.Id, b.Id }.Order(), group.ReportIds.Order());
        Assert.Equal("lab3", group.Location);
        Assert.DoesNotContain(c.Id, group.ReportIds);
    }

    [Fact]
    public void Different_fault_types_are_never_grouped()
    {
        var reports = new[] { Report("room 1", 9, type: "doors"), Report("room 1", 5, type: "plumbing") };
        Assert.Empty(RecurrenceAnalysis.Analyze(reports, NoVectors, 0.85, Now));
    }

    [Fact]
    public void Several_reports_of_one_failure_are_one_incident()
    {
        var reports = new[] { Report("room 1", 10), Report("room 1", 10), Report("room 1", 9) };
        var group = Assert.Single(RecurrenceAnalysis.Analyze(reports, NoVectors, 0.85, Now));
        Assert.Equal(3, group.ReportIds.Length);
        Assert.Equal(1, group.Incidents);
        Assert.Null(group.NextEstimate);
    }

    [Fact]
    public void Three_incidents_give_a_median_gap_range_and_next_date()
    {
        // Incidents start 60, 40 and 10 days ago, each fixed within two days: gaps 20 and 30.
        var reports = new[] { Report("room 1", 60, 59), Report("room 1", 40, 39), Report("room 1", 10, 9) };
        var group = Assert.Single(RecurrenceAnalysis.Analyze(reports, NoVectors, 0.85, Now));
        Assert.Equal(3, group.Incidents);
        Assert.Equal(25, group.MedianDays);
        Assert.Equal(20, group.MinDays);
        Assert.Equal(30, group.MaxDays);
        Assert.Equal(Now.AddDays(-10).AddDays(25), group.NextEstimate);
    }

    [Fact]
    public void A_report_during_an_unresolved_problem_joins_that_incident_however_late()
    {
        var reports = new[] { Report("room 1", 90), Report("room 1", 10) };
        var group = Assert.Single(RecurrenceAnalysis.Analyze(reports, NoVectors, 0.85, Now));
        Assert.Equal(1, group.Incidents);
    }

    [Fact]
    public void Two_incidents_are_not_enough_for_an_estimate()
    {
        var reports = new[] { Report("room 1", 60, 59), Report("room 1", 10, 9) };
        var group = Assert.Single(RecurrenceAnalysis.Analyze(reports, NoVectors, 0.85, Now));
        Assert.Equal(2, group.Incidents);
        Assert.Null(group.MedianDays);
        Assert.Null(group.NextEstimate);
    }
}
