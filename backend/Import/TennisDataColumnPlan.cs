using System.Text.Json;

namespace Tennis.Api.Import;

public sealed record UnmappedField(string Field, string Reason);

public sealed class SeasonHeaderPlan
{
    public required int WinnerCodeIndex { get; init; }
    public required IReadOnlyList<int> HomeIndexes { get; init; }
    public required IReadOnlyList<int> AwayIndexes { get; init; }
    public required IReadOnlyList<string> UnmappedColumns { get; init; }

    public static SeasonHeaderPlan Classify(string[] header)
    {
        var home = new List<int>();
        var away = new List<int>();
        var unmapped = new List<string>();
        var winner = -1;
        for (var index = 0; index < header.Length; index++)
        {
            var name = header[index];
            if (name == TennisDataRowMapper.WinnerCodeColumn)
            {
                winner = index;
            }
            else if (name.StartsWith("home_", StringComparison.Ordinal))
            {
                home.Add(index);
            }
            else if (name.StartsWith("away_", StringComparison.Ordinal))
            {
                away.Add(index);
            }
            else
            {
                unmapped.Add(name);
            }
        }

        return new SeasonHeaderPlan
        {
            WinnerCodeIndex = winner,
            HomeIndexes = home,
            AwayIndexes = away,
            UnmappedColumns = unmapped
        };
    }
}

public static class TennisDataBinding
{
    public const string OddsJson = "{}";

    public static readonly IReadOnlyList<UnmappedField> Unpublished =
    [
        new("Player.SourceId", "Each player has a stable id, but the season CSV column name is not published and no season file is available to read."),
        new("Player.Name", "The season CSV column name for the player name is not published."),
        new("Tournament.SourceId", "The season CSV column name for the tournament id is not published."),
        new("Tournament.Name", "The season CSV column name for the tournament name is not published."),
        new("TournamentEdition.Name", "The season CSV column name for the tournament edition name is not published."),
        new("TournamentEdition.Surface", "The season CSV column name for surface is not published."),
        new("TournamentEdition.Level", "Main Tour and Challenger are described as coverage, not as a published column name."),
        new("TournamentEdition.StartDate", "The season CSV column name for the tournament date is not published."),
        new("Match.PlayedOn", "The season CSV column name for the match date is not published."),
        new("MatchPlayer.Ranking", "rank is documented on the player-performance CSV, not as a season-match column name."),
        new("MatchPlayer.Odds", "Match-winner odds and first-set odds are documented as present, but their column names are not published.")
    ];

    public static string SerializeColumns(IReadOnlyList<string> columns) => JsonSerializer.Serialize(columns);

    public static IReadOnlyList<string> ReadColumns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }
}
