using Tennis.Api.Import;

namespace Tennis.Api.Models;

public record ImportRequest(string? Mode, int[]? Years);

public record PageResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public record PlayerResponse(Guid Id, string? SourceId, string Name);

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
    string? RawRecord);

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
    DateTimeOffset ImportedAt);

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
