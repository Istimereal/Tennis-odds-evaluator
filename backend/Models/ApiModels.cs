using Tennis.Api.Import;

namespace Tennis.Api.Models;

public record ImportRequest(string? Mode, int[]? Years);

public record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record PlayerResponse(
    Guid Id,
    string? SourceId,
    string Name,
    string? Nationality,
    int MatchCount,
    int Wins,
    int Losses);

public record TournamentResponse(
    Guid Id,
    string? SourceId,
    string Name,
    int? Year,
    string? Surface,
    string? Level,
    DateOnly? StartDate);

public record MatchPlayerResponse(
    Guid? PlayerId,
    string? SourceId,
    string? Name,
    string Side,
    bool? IsWinner,
    int? Ranking,
    string Statistics,
    string Odds);

public record MatchResponse(
    Guid Id,
    string SourceKey,
    int SourceYear,
    short? WinnerCode,
    DateOnly? PlayedOn,
    Guid? TournamentId,
    string? TournamentName,
    string? Surface,
    string? Level,
    IReadOnlyList<MatchPlayerResponse> Players,
    string? RawRecord,
    string? Round,
    string? Score,
    double? StatCompleteness);

public record YearImportCount(
    int Year,
    int RowsRead,
    int MatchesInserted,
    int PlayersInserted,
    int DuplicatesSkipped,
    int InvalidRowsSkipped,
    int MissingStatisticCells);

public record LevelCount(string Level, int Matches);

public record ImportSummaryResponse(
    bool DownloadAutomated,
    int FilesProcessed,
    int RowsRead,
    int MatchesInserted,
    int PlayersInserted,
    int DuplicatesSkipped,
    int InvalidRowsSkipped,
    int MissingStatisticCells,
    IReadOnlyList<YearImportCount> Years,
    IReadOnlyList<LevelCount> TournamentLevels,
    IReadOnlyList<string> UnmappedColumns,
    IReadOnlyList<UnmappedField> UnmappedFields,
    string Message);

public record ImportFileResponse(
    Guid Id,
    int? SourceYear,
    string OriginalFileName,
    string Sha256,
    string Status,
    int RowsRead,
    int MatchesInserted,
    int DuplicatesSkipped,
    int InvalidRowsSkipped,
    int MissingStatisticCells,
    IReadOnlyList<string> UnmappedColumns,
    string? Error,
    DateTimeOffset ImportedAt,
    IReadOnlyList<string> DetectedHeaders);

public record ImportBatchResponse(
    Guid Id,
    string Mode,
    string Provenance,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int FilesProcessed,
    int RowsRead,
    int MatchesInserted,
    int PlayersInserted,
    int DuplicatesSkipped,
    int InvalidRowsSkipped,
    int MissingStatisticCells,
    string? Error,
    IReadOnlyList<ImportFileResponse> Files);

public record SurfaceRecord(string Surface, int Wins, int Losses);

public record RecentMatchResponse(
    Guid Id,
    int SourceYear,
    DateOnly? MatchDate,
    DateOnly? TournamentStart,
    short? WinnerCode,
    bool? IsWinner,
    string? TournamentName,
    string? Surface,
    string? Level,
    string? Score);

public record PlayerDetailResponse(
    Guid Id,
    string? SourceId,
    string Name,
    string? Nationality,
    int MatchCount,
    int Wins,
    int Losses,
    IReadOnlyList<SurfaceRecord> SurfaceRecords,
    IReadOnlyList<RecentMatchResponse> RecentMatches);

public record TournamentEditionResponse(
    Guid Id,
    int Year,
    string? Name,
    string? Surface,
    string? Level,
    DateOnly? StartDate,
    int MatchCount);

public record TournamentDetailResponse(
    Guid Id,
    string? SourceId,
    string Name,
    IReadOnlyList<TournamentEditionResponse> Editions);

public record YearCount(int Year, int Count);

public record NamedCount(string Name, int Count);

public record ImportStatusResponse(
    Guid Id,
    string Mode,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int FilesProcessed,
    int MatchesInserted,
    int InvalidRowsSkipped,
    string? Error);

public record CompletenessResponse(
    int Matches,
    int MatchesWithWinner,
    int MatchesWithMatchDate,
    int MatchesWithTournamentStart,
    int MatchesWithSurface,
    int MatchesWithLevel,
    int MissingStatisticCells);

public record DashboardResponse(
    int Players,
    int Matches,
    int Tournaments,
    int ImportedSeasons,
    ImportStatusResponse? LatestBatch,
    ImportStatusResponse? LatestSuccessfulImport,
    int FailedFiles,
    int BatchesWithErrors,
    CompletenessResponse Completeness,
    IReadOnlyList<YearCount> MatchesByYear,
    IReadOnlyList<NamedCount> MatchesBySurface,
    IReadOnlyList<NamedCount> MatchesByLevel);

public record SeasonCoverage(
    int Year,
    int FilesImported,
    int RowsRead,
    int MatchesInserted,
    int DuplicatesSkipped,
    int InvalidRowsSkipped,
    int MissingStatisticCells,
    IReadOnlyList<string> UnmappedColumns);

public record CoverageResponse(
    IReadOnlyList<SeasonCoverage> Seasons,
    IReadOnlyList<UnmappedField> BlockedMappings);

public static class PageQuery
{
    public const int MaxPageSize = 100;

    public static string? Validate(int page, int pageSize)
    {
        if (page < 1)
        {
            return "Page must be 1 or greater.";
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            return $"Page size must be between 1 and {MaxPageSize}.";
        }

        return null;
    }
}
