using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Tennis.Api.Data.Entities;
using Match = Tennis.Api.Data.Entities.Match;

namespace Tennis.Api.Import;

public static class TennisStatisticColumns
{
    public const string Aces = "aces";
    public const string DoubleFaults = "double_faults";
    public const string ServicePointsWonPercent = "service_points_won_perc";
    public const string ReturnPointsWonPercent = "return_points_won_perc";
    public const string BreakPointsWonPercent = "break_points_won_perc";
    public const string BreakPointsConvertedPercent = "break_points_converted_perc";
    public const string BreakPointsSavedPercent = "break_points_saved_perc";

    // Beside home_rank / away_rank. Tour ranking points, not points won in the match.
    public const string RankingPoints = "points";

    public static readonly string[] TiebreakSuffixes = ["tiebreak", "tb", "tb_score", "tiebreak_score"];

    public static bool IsPlayerStatistic(string column)
    {
        if (column is "home_points" or "away_points")
        {
            return false;
        }

        return !IsSetStructure(column);
    }

    public static bool IsSetStructure(string column)
    {
        const string marker = "_set_";
        var at = column.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        var side = column[..at];
        if (side is not ("home" or "away"))
        {
            return false;
        }

        var rest = column[(at + marker.Length)..];
        var split = rest.IndexOf('_');
        if (split <= 0)
        {
            return false;
        }

        if (!int.TryParse(rest[..split], NumberStyles.None, CultureInfo.InvariantCulture, out var set) || set is < 1 or > 5)
        {
            return false;
        }

        var suffix = rest[(split + 1)..];
        return suffix is "score" or "tiebreak" or "tb" or "tb_score" or "tiebreak_score";
    }
}

public sealed record ParsedPlayerStatistics(
    int? Aces,
    int? DoubleFaults,
    decimal? ServicePointsWonPercent,
    decimal? ReturnPointsWonPercent,
    decimal? BreakPointsWonPercent,
    decimal? BreakPointsSavedPercent)
{
    public static ParsedPlayerStatistics Empty { get; } = new(null, null, null, null, null, null);
}

public sealed record ParsedSetScore(int SetNumber, int? HomeGames, int? AwayGames, int? HomeTiebreak, int? AwayTiebreak);

public static class TennisMatchStatistics
{
    private static readonly Regex CountPattern = new("^[0-9]{1,9}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PercentPattern = new("^[0-9]+(\\.[0-9]+)?$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static ParsedPlayerStatistics ParseSide(string[] header, string[] row, string side)
    {
        var won = ParsePercent(Cell(header, row, side + "_" + TennisStatisticColumns.BreakPointsWonPercent));
        var converted = ParsePercent(Cell(header, row, side + "_" + TennisStatisticColumns.BreakPointsConvertedPercent));
        return new ParsedPlayerStatistics(
            ParseCount(Cell(header, row, side + "_" + TennisStatisticColumns.Aces)),
            ParseCount(Cell(header, row, side + "_" + TennisStatisticColumns.DoubleFaults)),
            ParsePercent(Cell(header, row, side + "_" + TennisStatisticColumns.ServicePointsWonPercent)),
            ParsePercent(Cell(header, row, side + "_" + TennisStatisticColumns.ReturnPointsWonPercent)),
            won ?? converted,
            ParsePercent(Cell(header, row, side + "_" + TennisStatisticColumns.BreakPointsSavedPercent)));
    }

    public static IReadOnlyList<ParsedSetScore> ParseSets(string[] header, string[] row)
    {
        var sets = new List<ParsedSetScore>(5);
        for (var set = 1; set <= 5; set++)
        {
            var home = ParseCount(Cell(header, row, "home_set_" + set + "_score"));
            var away = ParseCount(Cell(header, row, "away_set_" + set + "_score"));
            int? homeTiebreak = null;
            int? awayTiebreak = null;
            foreach (var suffix in TennisStatisticColumns.TiebreakSuffixes)
            {
                homeTiebreak ??= ParseCount(Cell(header, row, "home_set_" + set + "_" + suffix));
                awayTiebreak ??= ParseCount(Cell(header, row, "away_set_" + set + "_" + suffix));
            }

            if (home is null && away is null && homeTiebreak is null && awayTiebreak is null)
            {
                continue;
            }

            sets.Add(new ParsedSetScore(set, home, away, homeTiebreak, awayTiebreak));
        }

        return sets;
    }

    public static int? ReadRankingPoints(string? rawJson, string side)
    {
        if (side is not ("home" or "away") || string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            var document = JsonNode.Parse(rawJson) as JsonObject;
            if (document is null || !document.TryGetPropertyValue(side + "_" + TennisStatisticColumns.RankingPoints, out var node) || node is null)
            {
                return null;
            }

            return ParseCount(node.GetValue<string?>());
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public static void ApplyParsed(MatchPlayer row, ParsedPlayerStatistics parsed)
    {
        var stats = row.PlayerStatistics;
        if (stats is null)
        {
            stats = new MatchPlayerStatistics
            {
                Id = Guid.NewGuid(),
                MatchPlayerId = row.Id
            };
            row.PlayerStatistics = stats;
        }

        stats.MatchPlayer = row;
        stats.Aces = parsed.Aces;
        stats.DoubleFaults = parsed.DoubleFaults;
        stats.ServicePointsWonPercent = parsed.ServicePointsWonPercent;
        stats.ReturnPointsWonPercent = parsed.ReturnPointsWonPercent;
        stats.BreakPointsWonPercent = parsed.BreakPointsWonPercent;
        stats.BreakPointsSavedPercent = parsed.BreakPointsSavedPercent;
        stats.FirstServePercent = null;
        stats.FirstServePointsWonPercent = null;
        stats.SecondServePointsWonPercent = null;
        stats.BreakPointsFaced = null;
        stats.BreakPointsSaved = null;
        stats.BreakPointChances = null;
        stats.BreakPointsConverted = null;
        stats.Winners = null;
        stats.UnforcedErrors = null;
        stats.NetPointsWon = null;
        stats.TotalPointsWon = null;
    }

    public static void ApplySets(Match match, IReadOnlyList<ParsedSetScore> incoming, ICollection<MatchSet> removed)
    {
        var byNumber = incoming
            .GroupBy(set => set.SetNumber)
            .ToDictionary(group => group.Key, group => group.Last());
        foreach (var existing in match.Sets.ToList())
        {
            if (!byNumber.Remove(existing.SetNumber, out var next))
            {
                // The caller deletes these rows. Removing them here as well queues a second delete.
                removed.Add(existing);
                continue;
            }

            existing.HomeGames = next.HomeGames;
            existing.AwayGames = next.AwayGames;
            existing.HomeTiebreak = next.HomeTiebreak;
            existing.AwayTiebreak = next.AwayTiebreak;
        }

        foreach (var next in byNumber.Values.OrderBy(set => set.SetNumber))
        {
            match.Sets.Add(new MatchSet
            {
                Id = Guid.NewGuid(),
                SetNumber = next.SetNumber,
                HomeGames = next.HomeGames,
                AwayGames = next.AwayGames,
                HomeTiebreak = next.HomeTiebreak,
                AwayTiebreak = next.AwayTiebreak
            });
        }
    }

    public static int? ParseCount(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (!CountPattern.IsMatch(text))
        {
            return null;
        }

        return int.Parse(text, CultureInfo.InvariantCulture);
    }

    public static decimal? ParsePercent(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim();
        if (text.EndsWith('%'))
        {
            text = text[..^1].Trim();
        }

        if (!PercentPattern.IsMatch(text))
        {
            return null;
        }

        var value = decimal.Parse(text, CultureInfo.InvariantCulture);
        var rounded = decimal.Round(value, 3, MidpointRounding.AwayFromZero);
        return rounded >= 10000m ? null : rounded;
    }

    private static string Cell(string[] header, string[] row, string name)
    {
        for (var index = 0; index < header.Length; index++)
        {
            if (header[index] != name)
            {
                continue;
            }

            return index < row.Length ? row[index].Trim() : "";
        }

        return "";
    }
}
