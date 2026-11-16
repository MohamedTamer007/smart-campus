using System.Text.RegularExpressions;

namespace SmartCampus.Infrastructure;

public sealed record RecurrenceReport(Guid Id, string FaultType, Guid BuildingId, Guid FloorId, string Description, string Details,
    DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt);

public sealed record RecurrenceGroup(string FaultType, Guid BuildingId, Guid FloorId, string? Location, Guid[] ReportIds,
    int Incidents, DateTimeOffset LastReported, double? MedianDays, double? MinDays, double? MaxDays, DateTimeOffset? NextEstimate);

/// <summary>
/// Finds repeated problems at "the same thing" and estimates when one may recur.
/// The estimate is plain statistics (median gap between incidents), not a prediction model.
/// </summary>
public static partial class RecurrenceAnalysis
{
    public const int MinimumIncidentsForEstimate = 3;
    private static readonly TimeSpan SameIncidentWindow = TimeSpan.FromHours(48);

    [GeneratedRegex(@"\b(room|rm|lab|hall|office|classroom|class|غرفه|معمل|قاعه|مدرج|مكتب|فصل)\s*[-#:.]?\s*([a-z]?\d{1,4}[a-z]?)\b")]
    private static partial Regex PlacePattern();

    /// <summary>Room, lab, hall, office or class number named in the text ("غرفة ٢٠٤" and "room 204" give the same key), or null.</summary>
    public static string? LocationKey(string text)
    {
        var match = PlacePattern().Match(SearchRelevance.Normalize(text));
        if (!match.Success) return null;
        var kind = match.Groups[1].Value switch
        {
            "room" or "rm" or "غرفه" => "room",
            "lab" or "معمل" => "lab",
            "hall" or "قاعه" or "مدرج" => "hall",
            "office" or "مكتب" => "office",
            _ => "class"
        };
        return kind + match.Groups[2].Value;
    }

    public static double Cosine(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0;
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++) { dot += a[i] * b[i]; na += a[i] * a[i]; nb += b[i] * b[i]; }
        return na == 0 || nb == 0 ? 0 : dot / Math.Sqrt(na * nb);
    }

    /// <summary>
    /// Groups reports of one fault type on one floor. A shared room/lab number always joins two reports;
    /// otherwise, equal wording or (when both have embeddings) similar wording joins them. Two different
    /// room numbers never join. Without embeddings only the text rules apply.
    /// </summary>
    public static RecurrenceGroup[] Analyze(IReadOnlyList<RecurrenceReport> reports, IReadOnlyDictionary<Guid, float[]> vectors,
        double similarity, DateTimeOffset now)
    {
        var groups = new List<RecurrenceGroup>();
        foreach (var partition in reports.GroupBy(r => (r.BuildingId, r.FloorId, r.FaultType)))
        {
            var items = partition.OrderBy(r => r.CreatedAt).ThenBy(r => r.Id).ToArray();
            var key = items.Select(r => LocationKey(r.Details) ?? LocationKey(r.Description)).ToArray();
            var wording = items.Select(r => string.Join(' ', SearchRelevance.Tokens(r.Details))).ToArray();
            var parent = Enumerable.Range(0, items.Length).ToArray();
            var rootKey = (string?[])key.Clone();
            int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
            // A group may never contain two different room numbers, even through a third,
            // unnumbered report that resembles both.
            void Union(int x, int y)
            {
                int rx = Find(x), ry = Find(y);
                if (rx == ry) return;
                if (rootKey[rx] is not null && rootKey[ry] is not null && rootKey[rx] != rootKey[ry]) return;
                parent[rx] = ry;
                rootKey[ry] ??= rootKey[rx];
            }
            // Strongest evidence first: same room number, then equal wording, then embedding similarity.
            var edges = new List<(int Rank, double Score, int I, int J)>();
            for (var i = 0; i < items.Length; i++)
                for (var j = i + 1; j < items.Length; j++)
                {
                    if (key[i] is not null && key[j] is not null) { if (key[i] == key[j]) edges.Add((0, 0, i, j)); continue; }
                    if (key[i] is null && key[j] is null && wording[i] == wording[j]) { edges.Add((1, 0, i, j)); continue; }
                    if (vectors.TryGetValue(items[i].Id, out var a) && vectors.TryGetValue(items[j].Id, out var b))
                    {
                        var cosine = Cosine(a, b);
                        if (cosine >= similarity) edges.Add((2, -cosine, i, j));
                    }
                }
            foreach (var edge in edges.OrderBy(e => e.Rank).ThenBy(e => e.Score).ThenBy(e => e.I).ThenBy(e => e.J)) Union(edge.I, edge.J);
            foreach (var component in Enumerable.Range(0, items.Length).GroupBy(Find))
            {
                var members = component.Select(i => items[i]).ToArray();
                if (members.Length < 2) continue;
                var location = component.Select(i => key[i]).Where(k => k is not null).GroupBy(k => k).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
                groups.Add(Summarize(members, location, now));
            }
        }
        return groups.OrderByDescending(g => g.ReportIds.Length).ThenByDescending(g => g.LastReported).ToArray();
    }

    private static RecurrenceGroup Summarize(RecurrenceReport[] members, string? location, DateTimeOffset now)
    {
        // Several people reporting one broken door the same day are one incident; a new incident
        // starts only after the previous one was fully resolved and a quiet window has passed.
        var starts = new List<DateTimeOffset>();
        DateTimeOffset lastCreated = default, latestResolved = default;
        var allResolved = false;
        foreach (var report in members)
        {
            var join = starts.Count > 0 && (report.CreatedAt - lastCreated <= SameIncidentWindow || !allResolved || latestResolved > report.CreatedAt);
            if (!join) { starts.Add(report.CreatedAt); allResolved = true; latestResolved = default; }
            lastCreated = report.CreatedAt;
            allResolved &= report.ResolvedAt.HasValue;
            if (report.ResolvedAt is { } resolved && resolved > latestResolved) latestResolved = resolved;
        }
        double? median = null, min = null, max = null;
        DateTimeOffset? next = null;
        if (starts.Count >= MinimumIncidentsForEstimate)
        {
            var gaps = starts.Zip(starts.Skip(1), (a, b) => (b - a).TotalDays).Order().ToArray();
            median = Math.Round(gaps.Length % 2 == 1 ? gaps[gaps.Length / 2] : (gaps[gaps.Length / 2 - 1] + gaps[gaps.Length / 2]) / 2, 1);
            min = Math.Round(gaps[0], 1);
            max = Math.Round(gaps[^1], 1);
            next = starts[^1].AddDays(median.Value);
        }
        var first = members[0];
        return new RecurrenceGroup(first.FaultType, first.BuildingId, first.FloorId, location, members.Select(m => m.Id).ToArray(),
            starts.Count, members.Max(m => m.CreatedAt), median, min, max, next);
    }
}
