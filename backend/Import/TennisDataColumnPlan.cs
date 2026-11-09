using System.Text.Json;

namespace Tennis.Api.Import;

public sealed record UnmappedField(string Field, string Reason);

public sealed class SeasonHeaderPlan
{
    private static readonly HashSet<string> MappedMatchColumns = new(StringComparer.Ordinal)
    {
        TennisDataRowMapper.WinnerCodeColumn,
        "match_id",
        "season_year",
        "tour_type_human",
        "date_timestamp",
        "date_human",
        "tournament",
        "round",
        "surface"
    };

    private static readonly HashSet<string> SideIdentityColumns = new(StringComparer.Ordinal)
    {
        "home_name",
        "away_name",
        "home_id",
        "away_id",
        "home_rank",
        "away_rank"
    };

    public required int WinnerCodeIndex { get; init; }
    public required IReadOnlyList<int> HomeIndexes { get; init; }
    public required IReadOnlyList<int> AwayIndexes { get; init; }
    public required IReadOnlyList<string> UnmappedColumns { get; init; }
    public required IReadOnlyDictionary<string, int> Indexes { get; init; }

    public int IndexOf(string name) => Indexes.TryGetValue(name, out var index) ? index : -1;

    public static SeasonHeaderPlan Classify(string[] header)
    {
        var home = new List<int>();
        var away = new List<int>();
        var unmapped = new List<string>();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var winner = -1;
        for (var index = 0; index < header.Length; index++)
        {
            var name = header[index];
            indexes[name] = index;
            if (name == TennisDataRowMapper.WinnerCodeColumn)
            {
                winner = index;
            }
            else if (SideIdentityColumns.Contains(name) || IsOddsColumn(name))
            {
                continue;
            }
            else if (name.StartsWith("home_", StringComparison.Ordinal))
            {
                home.Add(index);
            }
            else if (name.StartsWith("away_", StringComparison.Ordinal))
            {
                away.Add(index);
            }
            else if (!MappedMatchColumns.Contains(name))
            {
                unmapped.Add(name);
            }
        }

        return new SeasonHeaderPlan
        {
            WinnerCodeIndex = winner,
            HomeIndexes = home,
            AwayIndexes = away,
            UnmappedColumns = unmapped,
            Indexes = indexes
        };
    }

    public static bool IsOddsColumn(string name) =>
        name.StartsWith("home_odds_", StringComparison.Ordinal) || name.StartsWith("away_odds_", StringComparison.Ordinal);
}

public static class TennisDataBinding
{
    public const string OddsJson = "{}";

    public static readonly IReadOnlyList<UnmappedField> Unpublished =
    [
        new("TournamentEdition.StartDate", "date_timestamp is the match date. The season file has no tournament start date."),
        new("TournamentEdition.Participants", "The season file has no draw or participant count.")
    ];

    public static string SerializeColumns(IReadOnlyList<string> columns) => JsonSerializer.Serialize(columns);

    public static IReadOnlyList<string> ReadColumns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return document.RootElement
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? "")
                .Where(name => name.Length > 0)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
