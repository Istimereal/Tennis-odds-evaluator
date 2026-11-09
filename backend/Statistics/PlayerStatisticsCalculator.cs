namespace Tennis.Api.Statistics;

// Historical results from matches already stored. Future odds methods can call Calculate
// with an asOfDate and the match id being priced so that match stays out of its own history.
public static class PlayerStatisticsCalculator
{
    private static readonly string[] ExcludedMarkers = ["exhibition", "cancelled", "canceled", "unfinished", "invalid"];

    public static PlayerStatisticsReport Calculate(IReadOnlyList<PlayerMatchFact> matches, PlayerStatisticsRequest request)
    {
        var distinct = matches
            .GroupBy(match => match.MatchId)
            .Select(group => group.First())
            .ToArray();
        var statusExcluded = 0;
        var undecided = 0;
        var eligible = new List<PlayerMatchFact>();
        foreach (var match in distinct)
        {
            if (request.ExcludeMatchId is Guid excluded && match.MatchId == excluded)
            {
                continue;
            }

            if (!CompletedBefore(match, request.AsOfDate))
            {
                continue;
            }

            if (!InDateRange(match, request.From, request.To))
            {
                continue;
            }

            if (IsExcluded(match))
            {
                statusExcluded++;
                continue;
            }

            if (match.IsWinner is null)
            {
                undecided++;
                continue;
            }

            eligible.Add(match);
        }

        IEnumerable<PlayerMatchFact> scoped = eligible;
        if (request.Surfaces.Count > 0)
        {
            var selected = request.Surfaces.ToHashSet(StringComparer.Ordinal);
            scoped = scoped.Where(match => PlayerStatisticsTerms.CanonicalSurface(match.Surface) is string surface && selected.Contains(surface));
        }

        if (request.Level is not null)
        {
            scoped = scoped.Where(match => PlayerStatisticsTerms.CanonicalLevel(match.Level) == request.Level);
        }

        if (request.RankingGroup is not null)
        {
            var band = OpponentRankingBands.Find(request.RankingGroup);
            scoped = band is null
                ? []
                : scoped.Where(match => OpponentRankingBands.Contains(match.OpponentRanking, band));
        }

        var ordered = OrderNewest(scoped);
        if (request.RecentMatchCount is int recent)
        {
            ordered = ordered.Take(recent).ToList();
        }

        int? seasonYear = request.AsOfDate?.Year;
        if (seasonYear is null)
        {
            seasonYear = ordered.Select(match => match.PlayedOn?.Year).FirstOrDefault(year => year is not null);
        }
        var seasonMatches = seasonYear is int year
            ? ordered.Where(match => (match.PlayedOn?.Year ?? match.SourceYear) == year).ToList()
            : [];

        return new PlayerStatisticsReport(
            Summarize(ordered),
            SurfaceRows(ordered),
            LevelRows(ordered),
            RankingRows(ordered),
            OpponentRanking(ordered),
            [
                Form("last-5", "Last 5", null, ordered.Take(5).ToList()),
                Form("last-10", "Last 10", null, ordered.Take(10).ToList()),
                Form("last-20", "Last 20", null, ordered.Take(20).ToList()),
                Form("season", seasonYear is int season ? season + " season" : "Season", seasonYear, seasonMatches)
            ],
            [
                CountLine("aces", "Aces", ordered, stats => stats.Aces),
                CountLine("double-faults", "Double faults", ordered, stats => stats.DoubleFaults),
                RateLine(
                    "service-points-won",
                    "Service points won",
                    ordered,
                    _ => null,
                    _ => null,
                    stats => stats.ServicePointsWonPercent),
                RateLine(
                    "return-points-won",
                    "Return points won",
                    ordered,
                    _ => null,
                    _ => null,
                    stats => stats.ReturnPointsWonPercent),
                RateLine(
                    "break-points-won",
                    "Break points won",
                    ordered,
                    stats => stats.BreakPointsConverted,
                    stats => stats.BreakPointChances,
                    stats => stats.BreakPointsWonPercent),
                RateLine(
                    "break-points-saved",
                    "Break points saved",
                    ordered,
                    stats => stats.BreakPointsSaved,
                    stats => stats.BreakPointsFaced,
                    stats => stats.BreakPointsSavedPercent)
            ],
            Coverage(ordered, statusExcluded, undecided),
            new AppliedStatisticsFilters(
                request.AsOfDate,
                request.Surfaces,
                request.Level,
                request.From,
                request.To,
                request.RankingGroup,
                request.RecentMatchCount,
                request.ExcludeMatchId,
                seasonYear));
    }

    public static bool CompletedBefore(PlayerMatchFact match, DateOnly? asOf)
    {
        if (asOf is null)
        {
            return true;
        }

        if (match.CompletedAt is DateTimeOffset completed)
        {
            return DateOnly.FromDateTime(completed.UtcDateTime) < asOf.Value;
        }

        if (match.PlayedOn is DateOnly played)
        {
            return played < asOf.Value;
        }

        return false;
    }

    private static bool InDateRange(PlayerMatchFact match, DateOnly? from, DateOnly? to)
    {
        if (from is null && to is null)
        {
            return true;
        }

        if (match.PlayedOn is not DateOnly played)
        {
            return false;
        }

        if (from is DateOnly start && played < start)
        {
            return false;
        }

        if (to is DateOnly end && played > end)
        {
            return false;
        }

        return true;
    }

    private static bool IsExcluded(PlayerMatchFact match) =>
        ContainsExcluded(match.Status)
        || ContainsExcluded(match.StatusExtra)
        || ContainsExcluded(match.Level)
        || ContainsExcluded(match.TournamentName);

    private static bool ContainsExcluded(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.ToLowerInvariant();
        return ExcludedMarkers.Any(marker => text.Contains(marker, StringComparison.Ordinal));
    }

    private static List<PlayerMatchFact> OrderNewest(IEnumerable<PlayerMatchFact> matches) =>
        matches
            .OrderByDescending(match => match.PlayedOn.HasValue)
            .ThenByDescending(match => match.PlayedOn)
            .ThenByDescending(match => match.SourceYear)
            .ThenBy(match => match.SourceKey, StringComparer.Ordinal)
            .ToList();

    private static ResultsSummary Summarize(IReadOnlyList<PlayerMatchFact> matches)
    {
        var wins = matches.Count(match => match.IsWinner == true);
        var losses = matches.Count(match => match.IsWinner == false);
        return new ResultsSummary(wins + losses, wins, losses, Percentage(wins, wins + losses));
    }

    private static IReadOnlyList<SurfaceResults> SurfaceRows(IReadOnlyList<PlayerMatchFact> matches) =>
        PlayerStatisticsTerms.Surfaces
            .Select(surface => new SurfaceResults(
                surface,
                Summarize(matches.Where(match => PlayerStatisticsTerms.CanonicalSurface(match.Surface) == surface).ToList())))
            .ToArray();

    private static IReadOnlyList<LevelResults> LevelRows(IReadOnlyList<PlayerMatchFact> matches) =>
    [
        new("All", Summarize(matches)),
        new(PlayerStatisticsTerms.AtpTour, Summarize(matches.Where(match => PlayerStatisticsTerms.CanonicalLevel(match.Level) == PlayerStatisticsTerms.AtpTour).ToList())),
        new(PlayerStatisticsTerms.Challenger, Summarize(matches.Where(match => PlayerStatisticsTerms.CanonicalLevel(match.Level) == PlayerStatisticsTerms.Challenger).ToList()))
    ];

    private static IReadOnlyList<RankingGroupResults> RankingRows(IReadOnlyList<PlayerMatchFact> matches) =>
        OpponentRankingBands.All
            .Select(band => new RankingGroupResults(
                band.Key,
                Summarize(matches.Where(match => OpponentRankingBands.Contains(match.OpponentRanking, band)).ToList())))
            .ToArray();

    private static OpponentRankingSummary OpponentRanking(IReadOnlyList<PlayerMatchFact> matches)
    {
        var ranks = matches.Where(match => match.OpponentRanking is > 0).Select(match => match.OpponentRanking!.Value).ToArray();
        var missing = matches.Count - ranks.Length;
        if (ranks.Length == 0)
        {
            return new OpponentRankingSummary(null, 0, missing);
        }

        var average = decimal.Round(ranks.Aggregate(0m, (sum, rank) => sum + rank) / ranks.Length, 1, MidpointRounding.AwayFromZero);
        return new OpponentRankingSummary(average, ranks.Length, missing);
    }

    private static FormResults Form(string key, string label, int? seasonYear, IReadOnlyList<PlayerMatchFact> matches) =>
        new(key, label, seasonYear, Summarize(matches));

    private static StatLine CountLine(string key, string label, IReadOnlyList<PlayerMatchFact> matches, Func<PlayerMatchStatFact, int?> read)
    {
        var values = new List<int>();
        foreach (var match in matches)
        {
            if (match.Statistics is null)
            {
                continue;
            }

            if (read(match.Statistics) is int value && value >= 0)
            {
                values.Add(value);
            }
        }

        if (values.Count == 0)
        {
            return Empty(key, label, "count");
        }

        var total = values.Aggregate(0L, (sum, value) => sum + value);
        var perMatch = decimal.Round(total / (decimal)values.Count, 1, MidpointRounding.AwayFromZero);
        var storedTotal = total > int.MaxValue ? int.MaxValue : (int)total;
        return new StatLine(key, label, "count", storedTotal, StatBasis.StoredCounts, values.Count, storedTotal, perMatch, null, null, null);
    }

    private static StatLine RateLine(
        string key,
        string label,
        IReadOnlyList<PlayerMatchFact> matches,
        Func<PlayerMatchStatFact, int?> numerator,
        Func<PlayerMatchStatFact, int?> denominator,
        Func<PlayerMatchStatFact, decimal?> percentage)
    {
        var numeratorTotal = 0;
        var denominatorTotal = 0;
        var counted = 0;
        var percentages = new List<decimal>();
        foreach (var match in matches)
        {
            if (match.Statistics is not PlayerMatchStatFact stats)
            {
                continue;
            }

            var made = numerator(stats);
            var attempts = denominator(stats);
            if (made is int madeCount && attempts is int attemptCount && madeCount >= 0 && attemptCount > 0)
            {
                numeratorTotal += madeCount;
                denominatorTotal += attemptCount;
                counted++;
                continue;
            }

            if (percentage(stats) is decimal recorded)
            {
                percentages.Add(recorded);
            }
        }

        if (counted > 0)
        {
            var note = percentages.Count > 0
                ? "Recorded match percentages were kept out of the pooled count."
                : null;
            return new StatLine(
                key,
                label,
                "rate",
                Percentage(numeratorTotal, denominatorTotal),
                StatBasis.PooledCounts,
                counted,
                null,
                null,
                numeratorTotal,
                denominatorTotal,
                note);
        }

        if (percentages.Count > 0)
        {
            var average = decimal.Round(percentages.Sum() / percentages.Count, 1, MidpointRounding.AwayFromZero);
            return new StatLine(key, label, "rate", average, StatBasis.AverageOfPercentages, percentages.Count, null, null, null, null, null);
        }

        return Empty(key, label, "rate");
    }

    private static StatLine Empty(string key, string label, string kind) =>
        new(key, label, kind, null, StatBasis.None, 0, null, null, null, null, null);

    private static DataCoverage Coverage(IReadOnlyList<PlayerMatchFact> matches, int excluded, int undecided)
    {
        var missingRank = matches.Count(match => match.OpponentRanking is not > 0);
        var missingSurface = matches.Count(match => PlayerStatisticsTerms.CanonicalSurface(match.Surface) is null);
        return new DataCoverage(
            matches.Count,
            missingRank,
            missingSurface,
            matches.Count(match => match.Statistics?.Aces is null),
            matches.Count(match => match.Statistics?.DoubleFaults is null),
            MissingRate(matches, stats => stats.ServicePointsWonPercent, _ => null, _ => null),
            MissingRate(matches, stats => stats.ReturnPointsWonPercent, _ => null, _ => null),
            MissingRate(matches, stats => stats.BreakPointsWonPercent, stats => stats.BreakPointsConverted, stats => stats.BreakPointChances),
            MissingRate(matches, stats => stats.BreakPointsSavedPercent, stats => stats.BreakPointsSaved, stats => stats.BreakPointsFaced),
            undecided,
            excluded);
    }

    private static int MissingRate(
        IReadOnlyList<PlayerMatchFact> matches,
        Func<PlayerMatchStatFact, decimal?> percentage,
        Func<PlayerMatchStatFact, int?> numerator,
        Func<PlayerMatchStatFact, int?> denominator)
    {
        var missing = 0;
        foreach (var match in matches)
        {
            if (match.Statistics is not PlayerMatchStatFact stats)
            {
                missing++;
                continue;
            }

            var hasCounts = numerator(stats) is int made && denominator(stats) is int attempts && made >= 0 && attempts > 0;
            if (!hasCounts && percentage(stats) is null)
            {
                missing++;
            }
        }

        return missing;
    }

    private static decimal? Percentage(int numerator, int denominator)
    {
        if (denominator <= 0)
        {
            return null;
        }

        return decimal.Round(numerator * 100m / denominator, 1, MidpointRounding.AwayFromZero);
    }
}
