using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Tennis.Api.Import;

public sealed record MappedPlayerSide(
    string? SourceId,
    string? Name,
    int? Ranking,
    string OddsJson,
    string StatisticsJson);

public sealed record MappedSeasonRow(
    bool IsValid,
    short? WinnerCode,
    string RawJson,
    string SourceKey,
    string LegacySourceKey,
    string? MatchId,
    DateOnly? PlayedOn,
    string? TournamentName,
    string? Surface,
    string? Round,
    string? Score,
    string? TourLevel,
    int? SeasonYear,
    MappedPlayerSide Home,
    MappedPlayerSide Away,
    int MissingStatisticCells);

public static class TennisDataRowMapper
{
    public const string WinnerCodeColumn = "winner_code";

    private static readonly string[] DateFormats = ["dd MMM yyyy", "d MMM yyyy"];

    public static MappedSeasonRow Map(int year, SeasonHeaderPlan plan, string[] header, string[] row)
    {
        // Raw JSON stays in header order with the original cell text. The legacy source key is the hash of that document.
        var document = new JsonObject();
        for (var index = 0; index < header.Length; index++)
        {
            var raw = index < row.Length ? row[index] : "";
            document[header[index]] = raw.Length == 0 ? null : raw;
        }

        var rawJson = document.ToJsonString();
        var legacySourceKey = LegacySourceKey(year, rawJson);
        var matchId = NormalizeSourceId(Cell(row, plan.IndexOf("match_id")));
        var sourceKey = matchId ?? legacySourceKey;
        var missing = 0;
        var home = MapSide(header, row, "home", plan.HomeIndexes, ref missing);
        var away = MapSide(header, row, "away", plan.AwayIndexes, ref missing);
        var playedOn = PlayedOn(row, plan);
        var tournamentName = NormalizeText(Cell(row, plan.IndexOf("tournament")));
        var surface = NormalizeText(Cell(row, plan.IndexOf("surface")));
        var round = NormalizeText(Cell(row, plan.IndexOf("round")));
        var tourLevel = NormalizeText(Cell(row, plan.IndexOf("tour_type_human")))
            ?? NormalizeText(Cell(row, plan.IndexOf("tour_type")));
        int? seasonYear = int.TryParse(Cell(row, plan.IndexOf("season_year")), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedYear)
            ? parsedYear
            : null;
        var score = ScoreFromCells(row, plan);

        if (plan.WinnerCodeIndex < 0)
        {
            return Invalid(rawJson, sourceKey, legacySourceKey, matchId, playedOn, tournamentName, surface, round, score, tourLevel, seasonYear, home, away, missing);
        }

        var winnerText = plan.WinnerCodeIndex < row.Length ? row[plan.WinnerCodeIndex].Trim() : "";
        if (!short.TryParse(winnerText, NumberStyles.None, CultureInfo.InvariantCulture, out var winnerCode) || winnerCode is not (1 or 2))
        {
            return Invalid(rawJson, sourceKey, legacySourceKey, matchId, playedOn, tournamentName, surface, round, score, tourLevel, seasonYear, home, away, missing);
        }

        return new MappedSeasonRow(
            true,
            winnerCode,
            rawJson,
            sourceKey,
            legacySourceKey,
            matchId,
            playedOn,
            tournamentName,
            surface,
            round,
            score,
            tourLevel,
            seasonYear,
            home,
            away,
            missing);
    }

    public static string? ReadRound(string? rawJson) => ReadString(rawJson, "round");

    public static string? ReadScore(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            var document = JsonNode.Parse(rawJson) as JsonObject;
            if (document is null)
            {
                return null;
            }

            var parts = new List<string>();
            for (var set = 1; set <= 5; set++)
            {
                var home = JsonText(document, "home_set_" + set + "_score");
                var away = JsonText(document, "away_set_" + set + "_score");
                if (home.Length == 0 && away.Length == 0)
                {
                    continue;
                }

                parts.Add(home + "-" + away);
            }

            return parts.Count == 0 ? null : string.Join(' ', parts);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Trim().Normalize(NormalizationForm.FormC))
        {
            if (char.IsWhiteSpace(character))
            {
                if (builder.Length > 0)
                {
                    pendingSpace = true;
                }

                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    public static string? NormalizeSourceId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string LegacySourceKey(int year, string rawJson)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawJson))).ToLowerInvariant();
        return $"sha256:{year}:{hash}";
    }

    private static MappedSeasonRow Invalid(
        string rawJson,
        string sourceKey,
        string legacySourceKey,
        string? matchId,
        DateOnly? playedOn,
        string? tournamentName,
        string? surface,
        string? round,
        string? score,
        string? tourLevel,
        int? seasonYear,
        MappedPlayerSide home,
        MappedPlayerSide away,
        int missing)
    {
        return new MappedSeasonRow(
            false,
            null,
            rawJson,
            sourceKey,
            legacySourceKey,
            matchId,
            playedOn,
            tournamentName,
            surface,
            round,
            score,
            tourLevel,
            seasonYear,
            home,
            away,
            missing);
    }

    private static MappedPlayerSide MapSide(string[] header, string[] row, string side, IReadOnlyList<int> statisticIndexes, ref int missing)
    {
        var sourceId = NormalizeSourceId(Cell(row, Index(header, side + "_id")));
        var name = NormalizeText(Cell(row, Index(header, side + "_name")));
        int? ranking = int.TryParse(Cell(row, Index(header, side + "_rank")), NumberStyles.None, CultureInfo.InvariantCulture, out var parsedRank) && parsedRank > 0
            ? parsedRank
            : null;
        var odds = new JsonObject();
        var statistics = new JsonObject();
        foreach (var index in statisticIndexes)
        {
            var raw = index < row.Length ? row[index].Trim() : "";
            if (raw.Length == 0)
            {
                missing++;
            }

            statistics[StripSidePrefix(header[index])] = raw.Length == 0 ? null : raw;
        }

        for (var index = 0; index < header.Length; index++)
        {
            var column = header[index];
            if (!column.StartsWith(side + "_odds_", StringComparison.Ordinal))
            {
                continue;
            }

            var raw = index < row.Length ? row[index].Trim() : "";
            if (raw.Length == 0)
            {
                continue;
            }

            odds[column[(side.Length + "_odds_".Length)..]] = raw;
        }

        return new MappedPlayerSide(sourceId, name ?? sourceId, ranking, odds.ToJsonString(), statistics.ToJsonString());
    }

    private static DateOnly? PlayedOn(string[] row, SeasonHeaderPlan plan)
    {
        var timestamp = Cell(row, plan.IndexOf("date_timestamp"));
        if (long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var unix) && unix > 0)
        {
            return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime);
        }

        var human = Cell(row, plan.IndexOf("date_human"));
        if (DateOnly.TryParseExact(human, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? ScoreFromCells(string[] row, SeasonHeaderPlan plan)
    {
        var parts = new List<string>();
        for (var set = 1; set <= 5; set++)
        {
            var home = Cell(row, plan.IndexOf("home_set_" + set + "_score"));
            var away = Cell(row, plan.IndexOf("away_set_" + set + "_score"));
            if (home.Length == 0 && away.Length == 0)
            {
                continue;
            }

            parts.Add(home + "-" + away);
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }

    private static string? ReadString(string? rawJson, string name)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            var document = JsonNode.Parse(rawJson) as JsonObject;
            if (document is null)
            {
                return null;
            }

            var text = NormalizeText(JsonText(document, name));
            return text;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string JsonText(JsonObject document, string name)
    {
        if (!document.TryGetPropertyValue(name, out var node) || node is null)
        {
            return "";
        }

        return node.GetValue<string?>() ?? "";
    }

    private static string StripSidePrefix(string column)
    {
        if (column.StartsWith("home_", StringComparison.Ordinal))
        {
            return column["home_".Length..];
        }

        if (column.StartsWith("away_", StringComparison.Ordinal))
        {
            return column["away_".Length..];
        }

        return column;
    }

    private static int Index(string[] header, string name)
    {
        for (var index = 0; index < header.Length; index++)
        {
            if (header[index] == name)
            {
                return index;
            }
        }

        return -1;
    }

    private static string Cell(string[] row, int index)
    {
        if (index < 0 || index >= row.Length)
        {
            return "";
        }

        return row[index].Trim();
    }
}
