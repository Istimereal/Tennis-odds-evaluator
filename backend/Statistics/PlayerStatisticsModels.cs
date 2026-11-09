namespace Tennis.Api.Statistics;

public sealed record PlayerMatchStatFact(
    int? Aces,
    int? DoubleFaults,
    decimal? ServicePointsWonPercent,
    decimal? ReturnPointsWonPercent,
    decimal? BreakPointsWonPercent,
    decimal? BreakPointsSavedPercent,
    int? BreakPointsConverted,
    int? BreakPointChances,
    int? BreakPointsSaved,
    int? BreakPointsFaced);

public sealed record PlayerMatchFact(
    Guid MatchId,
    string SourceKey,
    int SourceYear,
    DateOnly? PlayedOn,
    DateTimeOffset? CompletedAt,
    string? Surface,
    string? Level,
    string? TournamentName,
    string? Status,
    string? StatusExtra,
    bool? IsWinner,
    int? OpponentRanking,
    PlayerMatchStatFact? Statistics);

public sealed record PlayerStatisticsRequest(
    DateOnly? AsOfDate,
    IReadOnlyList<string> Surfaces,
    string? Level,
    DateOnly? From,
    DateOnly? To,
    string? RankingGroup,
    int? RecentMatchCount,
    Guid? ExcludeMatchId)
{
    public static PlayerStatisticsRequest Unfiltered { get; } = new(null, [], null, null, null, null, null, null);

    public static bool TryCreate(
        DateOnly? asOfDate,
        IReadOnlyList<string>? surfaces,
        string? level,
        DateOnly? from,
        DateOnly? to,
        string? rankingGroup,
        int? recentMatchCount,
        Guid? excludeMatchId,
        out PlayerStatisticsRequest? request,
        out string? error)
    {
        request = null;
        error = null;
        var canonicalSurfaces = new List<string>();
        if (surfaces is not null)
        {
            foreach (var surface in surfaces)
            {
                if (string.IsNullOrWhiteSpace(surface))
                {
                    continue;
                }

                var canonical = PlayerStatisticsTerms.CanonicalSurface(surface);
                if (canonical is null)
                {
                    error = "Surface must be hard, indoor hard, clay, or grass.";
                    return false;
                }

                if (!canonicalSurfaces.Contains(canonical, StringComparer.Ordinal))
                {
                    canonicalSurfaces.Add(canonical);
                }
            }
        }

        if (canonicalSurfaces.Count > 2)
        {
            error = "Select one or two surfaces.";
            return false;
        }

        string? canonicalLevel = null;
        if (!string.IsNullOrWhiteSpace(level) && !level.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            canonicalLevel = PlayerStatisticsTerms.CanonicalLevelFilter(level);
            if (canonicalLevel is null)
            {
                error = "Level must be ATP Tour, Challenger, or all.";
                return false;
            }
        }

        string? canonicalGroup = null;
        if (!string.IsNullOrWhiteSpace(rankingGroup))
        {
            var band = OpponentRankingBands.Find(rankingGroup);
            if (band is null)
            {
                error = "Ranking group must be one of 1-3, top 5, top 10, top 10 without top 3, 11-20, 21-40, 41-70, 71-120, 121-200.";
                return false;
            }

            canonicalGroup = band.Key;
        }

        if (recentMatchCount is < 1 or > 500)
        {
            error = "Recent match count must be between 1 and 500.";
            return false;
        }

        if (from is DateOnly start && to is DateOnly end && start > end)
        {
            error = "The start of the date range must be on or before the end.";
            return false;
        }

        request = new PlayerStatisticsRequest(
            asOfDate,
            canonicalSurfaces,
            canonicalLevel,
            from,
            to,
            canonicalGroup,
            recentMatchCount,
            excludeMatchId);
        return true;
    }
}

public sealed record RankingBand(string Key, int Minimum, int Maximum);

public static class OpponentRankingBands
{
    public static readonly IReadOnlyList<RankingBand> All =
    [
        new("1-3", 1, 3),
        new("top 5", 1, 5),
        new("top 10", 1, 10),
        new("top 10 without top 3", 4, 10),
        new("11-20", 11, 20),
        new("21-40", 21, 40),
        new("41-70", 41, 70),
        new("71-120", 71, 120),
        new("121-200", 121, 200)
    ];

    public static RankingBand? Find(string text)
    {
        var trimmed = text.Trim();
        return All.FirstOrDefault(band => band.Key.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    public static bool Contains(int? ranking, RankingBand band) =>
        ranking is int rank && rank >= band.Minimum && rank <= band.Maximum;
}

public static class PlayerStatisticsTerms
{
    public const string Hard = "hard";
    public const string IndoorHard = "indoor hard";
    public const string Clay = "clay";
    public const string Grass = "grass";
    public const string AtpTour = "ATP Tour";
    public const string Challenger = "Challenger";

    public static readonly IReadOnlyList<string> Surfaces = [Hard, IndoorHard, Clay, Grass];

    public static string? CanonicalSurface(string? surface)
    {
        if (string.IsNullOrWhiteSpace(surface))
        {
            return null;
        }

        var text = surface.Trim().ToLowerInvariant().Replace('-', ' ').Replace('_', ' ');
        text = string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (text is "indoor hard" or "hard indoor")
        {
            return IndoorHard;
        }

        if (text is "hard")
        {
            return Hard;
        }

        if (text is "clay")
        {
            return Clay;
        }

        if (text is "grass")
        {
            return Grass;
        }

        return null;
    }

    public static string? CanonicalLevel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
        {
            return null;
        }

        var text = level.Trim().ToLowerInvariant();
        if (text.Contains("exhibition", StringComparison.Ordinal))
        {
            return null;
        }

        if (text.Contains("challenger", StringComparison.Ordinal))
        {
            return Challenger;
        }

        if (text.Contains("atp", StringComparison.Ordinal))
        {
            return AtpTour;
        }

        return null;
    }

    public static string? CanonicalLevelFilter(string level)
    {
        var text = level.Trim().ToLowerInvariant();
        if (text is "atp" or "atp tour")
        {
            return AtpTour;
        }

        if (text is "challenger")
        {
            return Challenger;
        }

        return null;
    }
}

public static class StatBasis
{
    public const string StoredCounts = "Sum of stored match counts";
    public const string PooledCounts = "Pooled from stored counts";
    public const string AverageOfPercentages = "Average of recorded match percentages";
    public const string None = "No stored values";
}

public sealed record ResultsSummary(int Matches, int Wins, int Losses, decimal? WinPercentage);

public sealed record SurfaceResults(string Surface, ResultsSummary Results);

public sealed record LevelResults(string Level, ResultsSummary Results);

public sealed record RankingGroupResults(string Group, ResultsSummary Results);

public sealed record OpponentRankingSummary(decimal? Average, int SampleSize, int Missing);

public sealed record FormResults(string Key, string Label, int? SeasonYear, ResultsSummary Results);

public sealed record StatLine(
    string Key,
    string Label,
    string Kind,
    decimal? Value,
    string Basis,
    int SampleSize,
    int? Total,
    decimal? PerMatch,
    int? Numerator,
    int? Denominator,
    string? Note);

public sealed record DataCoverage(
    int Matches,
    int MissingOpponentRanking,
    int MissingOrOtherSurface,
    int MissingAces,
    int MissingDoubleFaults,
    int MissingServicePointsWon,
    int MissingReturnPointsWon,
    int MissingBreakPointsWon,
    int MissingBreakPointsSaved,
    int UndecidedResults,
    int ExcludedMatches);

public sealed record AppliedStatisticsFilters(
    DateOnly? AsOfDate,
    IReadOnlyList<string> Surfaces,
    string? Level,
    DateOnly? From,
    DateOnly? To,
    string? RankingGroup,
    int? RecentMatchCount,
    Guid? ExcludeMatchId,
    int? SeasonYear);

public sealed record PlayerStatisticsReport(
    ResultsSummary Overall,
    IReadOnlyList<SurfaceResults> Surfaces,
    IReadOnlyList<LevelResults> Levels,
    IReadOnlyList<RankingGroupResults> RankingGroups,
    OpponentRankingSummary OpponentRanking,
    IReadOnlyList<FormResults> RecentForm,
    IReadOnlyList<StatLine> Statistics,
    DataCoverage Coverage,
    AppliedStatisticsFilters Filters);
