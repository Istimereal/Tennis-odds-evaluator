using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Data.Entities;

namespace Tennis.Api.Controllers;

// Temporary read-only sample of one stored match. The route answers only in Development.
[ApiController]
[Route("api/debug")]
public sealed class MatchSampleDebugController : ControllerBase
{
    private static readonly JsonSerializerOptions SampleJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public MatchSampleDebugController(AppDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [HttpGet("match-sample")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var matchId = await _db.Matches
            .AsNoTracking()
            .OrderBy(item => item.SourceKey)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (matchId is null)
        {
            return NotFound("No match was found.");
        }

        var match = await _db.Matches
            .AsNoTracking()
            .Include(item => item.Players)
            .ThenInclude(row => row.Player)
            .Include(item => item.TournamentEdition)
            .ThenInclude(edition => edition!.Tournament)
            .Include(item => item.ImportFile)
            .ThenInclude(file => file.ImportBatch)
            .FirstAsync(item => item.Id == matchId.Value, cancellationToken);

        return new JsonResult(MatchSampleDocument.From(match), SampleJson);
    }
}

public sealed record MatchSampleDocument(
    MatchSampleMatch Match,
    IReadOnlyList<MatchSampleMatchPlayer> MatchPlayers,
    MatchSampleTournamentEdition? TournamentEdition,
    MatchSampleImportFile? ImportFile)
{
    public static MatchSampleDocument From(Match match)
    {
        var edition = match.TournamentEdition;
        var file = match.ImportFile;
        return new MatchSampleDocument(
            new MatchSampleMatch(
                match.Id,
                match.SourceKey,
                match.SourceYear,
                match.SeasonFileName,
                match.WinnerCode,
                match.PlayedOn,
                match.TournamentEditionId,
                match.ImportFileId,
                ParseJson(match.RawRecord)),
            match.Players
                .OrderBy(row => row.Side, StringComparer.Ordinal)
                .Select(MatchSampleMatchPlayer.From)
                .ToArray(),
            edition is null ? null : MatchSampleTournamentEdition.From(edition),
            file is null ? null : MatchSampleImportFile.From(file));
    }

    internal static JsonNode? ParseJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return JsonValue.Create(json);
        }
    }
}

public sealed record MatchSampleMatch(
    Guid Id,
    string SourceKey,
    int SourceYear,
    string SeasonFileName,
    short? WinnerCode,
    DateOnly? PlayedOn,
    Guid? TournamentEditionId,
    Guid ImportFileId,
    JsonNode? RawRecord);

public sealed record MatchSampleMatchPlayer(
    Guid Id,
    Guid MatchId,
    Guid? PlayerId,
    string Side,
    bool? IsWinner,
    int? Ranking,
    JsonNode? Statistics,
    JsonNode? Odds,
    MatchSamplePlayer? Player)
{
    public static MatchSampleMatchPlayer From(MatchPlayer row)
    {
        return new MatchSampleMatchPlayer(
            row.Id,
            row.MatchId,
            row.PlayerId,
            row.Side,
            row.IsWinner,
            row.Ranking,
            MatchSampleDocument.ParseJson(row.Statistics),
            MatchSampleDocument.ParseJson(row.Odds),
            row.Player is null ? null : MatchSamplePlayer.From(row.Player));
    }
}

public sealed record MatchSamplePlayer(Guid Id, string? SourceId, string Name)
{
    public static MatchSamplePlayer From(Player player) => new(player.Id, player.SourceId, player.Name);
}

public sealed record MatchSampleTournament(Guid Id, string? SourceId, string Name)
{
    public static MatchSampleTournament From(Tournament tournament) => new(tournament.Id, tournament.SourceId, tournament.Name);
}

public sealed record MatchSampleTournamentEdition(
    Guid Id,
    Guid TournamentId,
    int Year,
    string? Surface,
    string? Level,
    string? Name,
    DateOnly? StartDate,
    MatchSampleTournament? Tournament)
{
    public static MatchSampleTournamentEdition From(TournamentEdition edition)
    {
        return new MatchSampleTournamentEdition(
            edition.Id,
            edition.TournamentId,
            edition.Year,
            edition.Surface,
            edition.Level,
            edition.Name,
            edition.StartDate,
            edition.Tournament is null ? null : MatchSampleTournament.From(edition.Tournament));
    }
}

public sealed record MatchSampleImportBatch(
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
    string? Error)
{
    public static MatchSampleImportBatch From(ImportBatch batch)
    {
        return new MatchSampleImportBatch(
            batch.Id,
            batch.Mode,
            batch.Provenance,
            batch.StartedAt,
            batch.FinishedAt,
            batch.FilesProcessed,
            batch.RowsRead,
            batch.MatchesInserted,
            batch.PlayersInserted,
            batch.DuplicatesSkipped,
            batch.InvalidRowsSkipped,
            batch.MissingStatisticCells,
            batch.Error);
    }
}

public sealed record MatchSampleImportFile(
    Guid Id,
    Guid ImportBatchId,
    int? SourceYear,
    string OriginalFileName,
    string Sha256,
    long ByteSize,
    string Provenance,
    JsonNode? HeaderJson,
    JsonNode? UnmappedColumns,
    string Status,
    int RowsRead,
    int MatchesInserted,
    int DuplicatesSkipped,
    int InvalidRowsSkipped,
    int MissingStatisticCells,
    string? Error,
    DateTimeOffset ImportedAt,
    MatchSampleImportBatch? ImportBatch)
{
    public static MatchSampleImportFile From(ImportFileRecord file)
    {
        return new MatchSampleImportFile(
            file.Id,
            file.ImportBatchId,
            file.SourceYear,
            file.OriginalFileName,
            file.Sha256,
            file.ByteSize,
            file.Provenance,
            MatchSampleDocument.ParseJson(file.HeaderJson),
            MatchSampleDocument.ParseJson(file.UnmappedColumns),
            file.Status,
            file.RowsRead,
            file.MatchesInserted,
            file.DuplicatesSkipped,
            file.InvalidRowsSkipped,
            file.MissingStatisticCells,
            file.Error,
            file.ImportedAt,
            file.ImportBatch is null ? null : MatchSampleImportBatch.From(file.ImportBatch));
    }
}
