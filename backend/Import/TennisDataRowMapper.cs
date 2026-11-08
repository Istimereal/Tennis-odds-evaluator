using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Tennis.Api.Import;

public sealed record MappedSeasonRow(
    bool IsValid,
    short? WinnerCode,
    string RawJson,
    string SourceKey,
    string HomeStatistics,
    string AwayStatistics,
    int MissingStatisticCells);

public static class TennisDataRowMapper
{
    public const string WinnerCodeColumn = "winner_code";

    public static MappedSeasonRow Map(int year, SeasonHeaderPlan plan, string[] header, string[] row)
    {
        var document = new JsonObject();
        for (var index = 0; index < header.Length; index++)
        {
            var raw = index < row.Length ? row[index] : "";
            document[header[index]] = raw.Length == 0 ? null : raw;
        }

        var missing = 0;
        var home = SideJson(header, row, plan.HomeIndexes, ref missing);
        var away = SideJson(header, row, plan.AwayIndexes, ref missing);
        var rawJson = document.ToJsonString();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawJson))).ToLowerInvariant();
        var sourceKey = $"sha256:{year}:{hash}";
        if (plan.WinnerCodeIndex < 0)
        {
            return new MappedSeasonRow(false, null, rawJson, sourceKey, home, away, missing);
        }

        var winnerText = plan.WinnerCodeIndex < row.Length ? row[plan.WinnerCodeIndex].Trim() : "";
        if (!short.TryParse(winnerText, out var winnerCode) || winnerCode is not (1 or 2))
        {
            return new MappedSeasonRow(false, null, rawJson, sourceKey, home, away, missing);
        }

        return new MappedSeasonRow(true, winnerCode, rawJson, sourceKey, home, away, missing);
    }

    private static string SideJson(string[] header, string[] row, IReadOnlyList<int> indexes, ref int missing)
    {
        var side = new JsonObject();
        foreach (var index in indexes)
        {
            var raw = index < row.Length ? row[index] : "";
            if (raw.Length == 0)
            {
                missing++;
            }

            side[header[index]] = raw.Length == 0 ? null : raw;
        }

        return side.ToJsonString();
    }
}
