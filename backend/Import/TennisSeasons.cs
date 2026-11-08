namespace Tennis.Api.Import;

public static class ImportModes
{
    public const string Backfill = "backfill";
    public const string Incremental = "incremental";
    public const string Refresh = "refresh";

    public static bool TryNormalize(string? mode, out string normalized)
    {
        normalized = string.IsNullOrWhiteSpace(mode) ? Backfill : mode.Trim().ToLowerInvariant();
        return normalized is Backfill or Incremental or Refresh;
    }
}

public static class TennisSeasons
{
    public const string Provenance = "https://tennisdata.app/downloads/";
    public static readonly int[] Supported = [2021, 2022, 2023, 2024, 2025, 2026];

    public static string? Validate(IReadOnlyList<int> years)
    {
        if (years.Contains(2020))
        {
            return "2020 is a coverage gap for TennisData.app free ATP season files and is not imported.";
        }

        var rejected = years.Where(year => !Supported.Contains(year)).Distinct().Order().ToArray();
        if (rejected.Length > 0)
        {
            return "Only ATP seasons 2021-2026 are imported. Rejected years: " + string.Join(", ", rejected) + ".";
        }

        return null;
    }
}

public sealed record SeasonFile(string Path, string FileName, int Year);
