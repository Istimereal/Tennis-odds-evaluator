using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Data.Entities;
using Tennis.Api.Import;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class TennisDataImportService
{
    private const int BatchSize = 500;

    private readonly AppDbContext _db;
    private readonly TennisDataDownloadClient _download;
    private readonly TennisSeasonFileSource _files;

    public TennisDataImportService(AppDbContext db, TennisDataDownloadClient download, TennisSeasonFileSource files)
    {
        _db = db;
        _download = download;
        _files = files;
    }

    public async Task<ImportOutcome> ImportAsync(string? mode, IReadOnlyList<int>? years, bool download, CancellationToken cancellationToken)
    {
        if (!ImportModes.TryNormalize(mode, out var normalizedMode))
        {
            return ImportOutcome.Fail(400, "Mode must be backfill, incremental, or refresh.");
        }

        var selectedYears = years is null || years.Count == 0 ? TennisSeasons.Supported : years.Distinct().Order().ToArray();
        var yearError = TennisSeasons.Validate(selectedYears);
        if (yearError is not null)
        {
            return ImportOutcome.Fail(400, yearError);
        }

        var downloadResult = download
            ? await _download.TryDownloadAsync(selectedYears, normalizedMode, _files.DirectoryPath, cancellationToken)
            : new DownloadResult(false, [], "Download was not requested. Local season files were used.");

        var seasonFiles = _files.Find(selectedYears);
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(),
            Mode = normalizedMode,
            Provenance = TennisSeasons.Provenance,
            StartedAt = DateTimeOffset.UtcNow
        };
        var yearCounts = selectedYears.ToDictionary(year => year, _ => new MutableYearCount());
        var filesProcessed = 0;
        var rowsRead = 0;
        var matchesInserted = 0;
        var matchesUpdated = 0;
        var playersInserted = 0;
        var tournamentsInserted = 0;
        var duplicatesSkipped = 0;
        var invalidRowsSkipped = 0;
        var missingStatisticCells = 0;
        var unmappedColumns = new HashSet<string>(StringComparer.Ordinal);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            foreach (var seasonFile in seasonFiles)
            {
                var outcome = await ImportFileAsync(batch.Id, seasonFile, cancellationToken);
                var count = yearCounts[seasonFile.Year];
                count.RowsRead += outcome.RowsRead;
                count.MatchesInserted += outcome.MatchesInserted;
                count.MatchesUpdated += outcome.MatchesUpdated;
                count.PlayersInserted += outcome.PlayersInserted;
                count.TournamentsInserted += outcome.TournamentsInserted;
                count.DuplicatesSkipped += outcome.DuplicatesSkipped;
                count.InvalidRowsSkipped += outcome.InvalidRowsSkipped;
                count.MissingStatisticCells += outcome.MissingStatisticCells;
                filesProcessed++;
                rowsRead += outcome.RowsRead;
                matchesInserted += outcome.MatchesInserted;
                matchesUpdated += outcome.MatchesUpdated;
                playersInserted += outcome.PlayersInserted;
                tournamentsInserted += outcome.TournamentsInserted;
                duplicatesSkipped += outcome.DuplicatesSkipped;
                invalidRowsSkipped += outcome.InvalidRowsSkipped;
                missingStatisticCells += outcome.MissingStatisticCells;
                foreach (var column in outcome.UnmappedColumns)
                {
                    unmappedColumns.Add(column);
                }
            }

            var storedBatch = await _db.ImportBatches.FirstAsync(item => item.Id == batch.Id, cancellationToken);
            storedBatch.FilesProcessed = filesProcessed;
            storedBatch.RowsRead = rowsRead;
            storedBatch.MatchesInserted = matchesInserted;
            storedBatch.PlayersInserted = playersInserted;
            storedBatch.DuplicatesSkipped = duplicatesSkipped;
            storedBatch.InvalidRowsSkipped = invalidRowsSkipped;
            storedBatch.MissingStatisticCells = missingStatisticCells;
            storedBatch.FinishedAt = DateTimeOffset.UtcNow;
            storedBatch.Error = seasonFiles.Count == 0 ? downloadResult.Message : null;
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var levels = await LevelCountsAsync(cancellationToken);
        var unmappedColumnList = unmappedColumns.OrderBy(column => column, StringComparer.Ordinal).ToArray();
        var message = BuildMessage(downloadResult.Message, seasonFiles.Count, matchesUpdated, playersInserted, tournamentsInserted, unmappedColumnList);
        return ImportOutcome.Ok(new ImportSummaryResponse(
            downloadResult.Automated,
            filesProcessed,
            rowsRead,
            matchesInserted,
            playersInserted,
            duplicatesSkipped,
            invalidRowsSkipped,
            missingStatisticCells,
            yearCounts.OrderBy(item => item.Key).Select(item => item.Value.ToResponse(item.Key)).ToArray(),
            levels,
            unmappedColumnList,
            TennisDataBinding.Unpublished,
            message));
    }

    private async Task<FileOutcome> ImportFileAsync(Guid batchId, SeasonFile seasonFile, CancellationToken cancellationToken)
    {
        var hash = await HashAsync(seasonFile.Path, cancellationToken);
        var byteSize = new FileInfo(seasonFile.Path).Length;

        List<string[]> table;
        try
        {
            table = TennisCsvReader.ReadRows(seasonFile.Path);
        }
        catch (InvalidDataException exception)
        {
            await RejectAsync(batchId, seasonFile, hash, byteSize, "[]", "[]", exception.Message, cancellationToken);
            return RejectedFile([]);
        }

        if (table.Count == 0)
        {
            await RejectAsync(batchId, seasonFile, hash, byteSize, "[]", "[]", "The CSV has no header row.", cancellationToken);
            return RejectedFile([]);
        }

        var header = table[0];
        var headerJson = JsonSerializer.Serialize(header);
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Length || header.Any(name => name.Length == 0))
        {
            await RejectAsync(batchId, seasonFile, hash, byteSize, headerJson, "[]", "The header has a blank or duplicate column name.", cancellationToken);
            return RejectedFile([]);
        }

        var plan = SeasonHeaderPlan.Classify(header);
        var unmappedJson = TennisDataBinding.SerializeColumns(plan.UnmappedColumns);
        if (plan.WinnerCodeIndex < 0)
        {
            await RejectAsync(
                batchId,
                seasonFile,
                hash,
                byteSize,
                headerJson,
                unmappedJson,
                "The header has no winner_code column, so this is not a TennisData.app season file. Unmapped columns are stored and were not dropped.",
                cancellationToken);
            return RejectedFile(plan.UnmappedColumns);
        }

        var fileRecord = new ImportFileRecord
        {
            Id = Guid.NewGuid(),
            ImportBatchId = batchId,
            SourceYear = seasonFile.Year,
            OriginalFileName = seasonFile.FileName,
            Sha256 = hash,
            ByteSize = byteSize,
            Provenance = TennisSeasons.Provenance,
            HeaderJson = headerJson,
            UnmappedColumns = unmappedJson,
            Status = "imported",
            ImportedAt = DateTimeOffset.UtcNow
        };
        _db.ImportFiles.Add(fileRecord);
        await _db.SaveChangesAsync(cancellationToken);

        var players = await _db.Players
            .Where(player => player.SourceId != null)
            .ToDictionaryAsync(player => player.SourceId!, StringComparer.Ordinal, cancellationToken);
        var tournaments = await _db.Tournaments
            .Where(tournament => tournament.SourceId != null)
            .ToDictionaryAsync(tournament => tournament.SourceId!, StringComparer.Ordinal, cancellationToken);
        var editions = (await _db.TournamentEditions.ToListAsync(cancellationToken))
            .ToDictionary(edition => (edition.TournamentId, edition.Year));
        var existingMatches = await _db.Matches
            .Include(match => match.Players)
            .Where(match => match.SourceYear == seasonFile.Year)
            .ToListAsync(cancellationToken);
        var byKey = new Dictionary<string, Match>(StringComparer.Ordinal);
        var byMatchId = new Dictionary<string, Match>(StringComparer.Ordinal);
        foreach (var existing in existingMatches)
        {
            byKey[existing.SourceKey] = existing;
            var storedMatchId = ReadMatchId(existing.RawRecord);
            if (storedMatchId is not null)
            {
                byMatchId.TryAdd(storedMatchId, existing);
            }
        }

        var touched = new HashSet<Guid>();
        var rowsRead = 0;
        var inserted = 0;
        var updated = 0;
        var playersInserted = 0;
        var tournamentsInserted = 0;
        var duplicates = 0;
        var invalid = 0;
        var missing = 0;
        var pending = 0;

        foreach (var row in table.Skip(1))
        {
            if (row.Length != header.Length)
            {
                invalid++;
                continue;
            }

            var mapped = TennisDataRowMapper.Map(seasonFile.Year, plan, header, row);
            rowsRead++;
            missing += mapped.MissingStatisticCells;
            if (!mapped.IsValid)
            {
                invalid++;
                continue;
            }

            var match = FindExisting(mapped, byMatchId, byKey);
            if (match is not null && !touched.Add(match.Id))
            {
                duplicates++;
                continue;
            }

            var home = ResolvePlayer(mapped.Home, players, ref playersInserted);
            var away = ResolvePlayer(mapped.Away, players, ref playersInserted);
            var edition = ResolveEdition(mapped, seasonFile.Year, tournaments, editions, ref tournamentsInserted);
            if (match is null)
            {
                match = new Match
                {
                    Id = Guid.NewGuid(),
                    SourceYear = seasonFile.Year,
                    ImportFileId = fileRecord.Id
                };
                _db.Matches.Add(match);
                touched.Add(match.Id);
                inserted++;
            }
            else
            {
                updated++;
            }

            match.SourceKey = mapped.SourceKey;
            match.SourceYear = seasonFile.Year;
            match.SeasonFileName = seasonFile.FileName;
            match.WinnerCode = mapped.WinnerCode;
            match.PlayedOn = mapped.PlayedOn;
            match.RawRecord = mapped.RawJson;
            match.ImportFileId = fileRecord.Id;
            match.TournamentEdition = edition;
            byKey[mapped.SourceKey] = match;
            if (mapped.MatchId is not null)
            {
                byMatchId[mapped.MatchId] = match;
            }

            ApplySide(match, "home", mapped.Home, mapped.WinnerCode == 1, home);
            ApplySide(match, "away", mapped.Away, mapped.WinnerCode == 2, away);
            pending++;
            if (pending < BatchSize)
            {
                continue;
            }

            await _db.SaveChangesAsync(cancellationToken);
            pending = 0;
        }

        if (pending > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var stored = await _db.ImportFiles.FirstAsync(file => file.Id == fileRecord.Id, cancellationToken);
        stored.RowsRead = rowsRead;
        stored.MatchesInserted = inserted;
        stored.DuplicatesSkipped = duplicates;
        stored.InvalidRowsSkipped = invalid;
        stored.MissingStatisticCells = missing;
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();
        return new FileOutcome(rowsRead, inserted, updated, playersInserted, tournamentsInserted, duplicates, invalid, missing, plan.UnmappedColumns);
    }

    private static Match? FindExisting(MappedSeasonRow mapped, Dictionary<string, Match> byMatchId, Dictionary<string, Match> byKey)
    {
        if (mapped.MatchId is not null && byMatchId.TryGetValue(mapped.MatchId, out var byId))
        {
            return byId;
        }

        if (byKey.TryGetValue(mapped.LegacySourceKey, out var byLegacy))
        {
            return byLegacy;
        }

        if (byKey.TryGetValue(mapped.SourceKey, out var bySource))
        {
            return bySource;
        }

        return null;
    }

    private Player? ResolvePlayer(MappedPlayerSide side, Dictionary<string, Player> players, ref int created)
    {
        if (side.SourceId is null && side.Name is null)
        {
            return null;
        }

        var sourceId = side.SourceId ?? "name:" + side.Name!.ToLowerInvariant();
        if (players.TryGetValue(sourceId, out var player))
        {
            if (player.Name.Length == 0 && side.Name is not null)
            {
                player.Name = side.Name;
            }

            return player;
        }

        player = new Player
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            Name = side.Name ?? sourceId
        };
        _db.Players.Add(player);
        players[sourceId] = player;
        created++;
        return player;
    }

    private TournamentEdition? ResolveEdition(
        MappedSeasonRow mapped,
        int fileYear,
        Dictionary<string, Tournament> tournaments,
        Dictionary<(Guid TournamentId, int Year), TournamentEdition> editions,
        ref int tournamentsInserted)
    {
        if (mapped.TournamentName is null)
        {
            return null;
        }

        if (!tournaments.TryGetValue(mapped.TournamentName, out var tournament))
        {
            tournament = new Tournament
            {
                Id = Guid.NewGuid(),
                SourceId = mapped.TournamentName,
                Name = mapped.TournamentName
            };
            _db.Tournaments.Add(tournament);
            tournaments[mapped.TournamentName] = tournament;
            tournamentsInserted++;
        }

        var year = mapped.SeasonYear ?? fileYear;
        if (!editions.TryGetValue((tournament.Id, year), out var edition))
        {
            edition = new TournamentEdition
            {
                Id = Guid.NewGuid(),
                Tournament = tournament,
                Year = year,
                Name = mapped.TournamentName,
                Surface = mapped.Surface,
                Level = mapped.TourLevel
            };
            _db.TournamentEditions.Add(edition);
            editions[(tournament.Id, year)] = edition;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(edition.Name))
            {
                edition.Name = mapped.TournamentName;
            }

            if (string.IsNullOrWhiteSpace(edition.Surface))
            {
                edition.Surface = mapped.Surface;
            }

            if (string.IsNullOrWhiteSpace(edition.Level))
            {
                edition.Level = mapped.TourLevel;
            }
        }

        return edition;
    }

    private static void ApplySide(Match match, string sideName, MappedPlayerSide side, bool isWinner, Player? player)
    {
        var row = match.Players.FirstOrDefault(item => item.Side == sideName);
        if (row is null)
        {
            row = new MatchPlayer
            {
                Id = Guid.NewGuid(),
                Side = sideName
            };
            match.Players.Add(row);
        }

        row.Player = player;
        if (player is null)
        {
            row.PlayerId = null;
        }

        row.IsWinner = isWinner;
        row.Ranking = side.Ranking;
        row.Statistics = side.StatisticsJson;
        row.Odds = side.OddsJson;
    }

    private static string? ReadMatchId(string rawJson)
    {
        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (document.RootElement.TryGetProperty("match_id", out var value) && value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static FileOutcome RejectedFile(IReadOnlyList<string> unmappedColumns) =>
        new(0, 0, 0, 0, 0, 0, 1, 0, unmappedColumns);

    private async Task RejectAsync(
        Guid batchId,
        SeasonFile seasonFile,
        string hash,
        long byteSize,
        string headerJson,
        string unmappedColumns,
        string error,
        CancellationToken cancellationToken)
    {
        _db.ImportFiles.Add(new ImportFileRecord
        {
            Id = Guid.NewGuid(),
            ImportBatchId = batchId,
            SourceYear = seasonFile.Year,
            OriginalFileName = seasonFile.FileName,
            Sha256 = hash,
            ByteSize = byteSize,
            Provenance = TennisSeasons.Provenance,
            HeaderJson = headerJson,
            UnmappedColumns = unmappedColumns,
            Status = "rejected",
            Error = error,
            InvalidRowsSkipped = 1,
            ImportedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<LevelCount>> LevelCountsAsync(CancellationToken cancellationToken)
    {
        var mapped = await _db.Matches
            .AsNoTracking()
            .Where(match => match.TournamentEdition != null)
            .GroupBy(match => match.TournamentEdition!.Level)
            .Select(group => new LevelCount(group.Key ?? "unknown", group.Count()))
            .ToListAsync(cancellationToken);
        var unmapped = await _db.Matches.CountAsync(match => match.TournamentEditionId == null, cancellationToken);
        if (unmapped > 0)
        {
            mapped.Add(new LevelCount("unmapped", unmapped));
        }

        return mapped;
    }

    private static string BuildMessage(
        string downloadMessage,
        int fileCount,
        int matchesUpdated,
        int playersInserted,
        int tournamentsInserted,
        IReadOnlyList<string> unmappedColumns)
    {
        var files = fileCount == 0
            ? " No yyyy-atp-season.csv files for 2021-2026 were in Data/source."
            : $" Existing matches updated: {matchesUpdated}. Players created: {playersInserted}. Tournaments created: {tournamentsInserted}. The file is read again when its SHA-256 matches a previous import. Each match is updated by match_id, including a row whose stored source key is still the earlier sha256 digest.";
        var seen = unmappedColumns.Count == 0
            ? " No extra CSV columns were seen."
            : " CSV columns kept only in RawRecord: " + string.Join(", ", unmappedColumns) + ".";
        var unpublished = string.Join(" ", TennisDataBinding.Unpublished.Select(field => field.Field + " (" + field.Reason + ")"));
        return downloadMessage
            + files
            + " Mapped: match_id to Match.SourceKey; home_id and away_id to Player.SourceId; home_name and away_name to Player.Name; both sides to MatchPlayer; tournament to Tournament.Name; surface to TournamentEdition.Surface; tour_type_human to TournamentEdition.Level; date_timestamp to Match.PlayedOn; round from RawRecord; home_rank and away_rank to MatchPlayer.Ranking; odds columns to MatchPlayer.Odds; remaining home_ and away_ columns to MatchPlayer statistics; every column to Match.RawRecord."
            + seen
            + " Not stored on their own columns: "
            + unpublished;
    }

    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class MutableYearCount
    {
        public int RowsRead { get; set; }
        public int MatchesInserted { get; set; }
        public int MatchesUpdated { get; set; }
        public int PlayersInserted { get; set; }
        public int TournamentsInserted { get; set; }
        public int DuplicatesSkipped { get; set; }
        public int InvalidRowsSkipped { get; set; }
        public int MissingStatisticCells { get; set; }

        public YearImportCount ToResponse(int year) => new(
            year,
            RowsRead,
            MatchesInserted,
            PlayersInserted,
            DuplicatesSkipped,
            InvalidRowsSkipped,
            MissingStatisticCells);
    }

    private sealed record FileOutcome(
        int RowsRead,
        int MatchesInserted,
        int MatchesUpdated,
        int PlayersInserted,
        int TournamentsInserted,
        int DuplicatesSkipped,
        int InvalidRowsSkipped,
        int MissingStatisticCells,
        IReadOnlyList<string> UnmappedColumns);
}

public sealed class ImportOutcome
{
    public int StatusCode { get; private init; }
    public string? Error { get; private init; }
    public ImportSummaryResponse? Summary { get; private init; }

    public static ImportOutcome Fail(int statusCode, string error) => new() { StatusCode = statusCode, Error = error };

    public static ImportOutcome Ok(ImportSummaryResponse summary) => new() { StatusCode = 200, Summary = summary };
}
