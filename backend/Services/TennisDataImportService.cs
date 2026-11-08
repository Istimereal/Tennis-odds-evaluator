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
                count.DuplicatesSkipped += outcome.DuplicatesSkipped;
                count.InvalidRowsSkipped += outcome.InvalidRowsSkipped;
                count.MissingStatisticCells += outcome.MissingStatisticCells;
                filesProcessed++;
                rowsRead += outcome.RowsRead;
                matchesInserted += outcome.MatchesInserted;
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
        var message = BuildMessage(downloadResult.Message, seasonFiles.Count, unmappedColumnList);
        return ImportOutcome.Ok(new ImportSummaryResponse(
            downloadResult.Automated,
            filesProcessed,
            rowsRead,
            matchesInserted,
            0,
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
        var previous = await _db.ImportFiles
            .AsNoTracking()
            .Where(file => file.Sha256 == hash && file.Status == "imported")
            .OrderByDescending(file => file.ImportedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (previous is not null)
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
                HeaderJson = previous.HeaderJson,
                UnmappedColumns = previous.UnmappedColumns,
                Status = "skipped-unchanged",
                RowsRead = previous.RowsRead,
                DuplicatesSkipped = previous.MatchesInserted,
                ImportedAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(cancellationToken);
            return new FileOutcome(0, 0, previous.MatchesInserted, 0, 0, TennisDataBinding.ReadColumns(previous.UnmappedColumns));
        }

        List<string[]> table;
        try
        {
            table = TennisCsvReader.ReadRows(seasonFile.Path);
        }
        catch (InvalidDataException exception)
        {
            await RejectAsync(batchId, seasonFile, hash, byteSize, "[]", "[]", exception.Message, cancellationToken);
            return new FileOutcome(0, 0, 0, 1, 0, []);
        }

        if (table.Count == 0)
        {
            await RejectAsync(batchId, seasonFile, hash, byteSize, "[]", "[]", "The CSV has no header row.", cancellationToken);
            return new FileOutcome(0, 0, 0, 1, 0, []);
        }

        var header = table[0];
        var headerJson = JsonSerializer.Serialize(header);
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Length || header.Any(name => name.Length == 0))
        {
            await RejectAsync(batchId, seasonFile, hash, byteSize, headerJson, "[]", "The header has a blank or duplicate column name.", cancellationToken);
            return new FileOutcome(0, 0, 0, 1, 0, []);
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
            return new FileOutcome(0, 0, 0, 1, 0, plan.UnmappedColumns);
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

        var existingKeys = await _db.Matches
            .Where(match => match.SourceYear == seasonFile.Year)
            .Select(match => match.SourceKey)
            .ToListAsync(cancellationToken);
        var knownKeys = existingKeys.ToHashSet(StringComparer.Ordinal);
        var rowsRead = 0;
        var inserted = 0;
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

            if (!knownKeys.Add(mapped.SourceKey))
            {
                duplicates++;
                continue;
            }

            var match = new Match
            {
                Id = Guid.NewGuid(),
                SourceKey = mapped.SourceKey,
                SourceYear = seasonFile.Year,
                SeasonFileName = seasonFile.FileName,
                WinnerCode = mapped.WinnerCode,
                RawRecord = mapped.RawJson,
                ImportFileId = fileRecord.Id
            };
            match.Players.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                Side = "home",
                IsWinner = mapped.WinnerCode == 1,
                Statistics = mapped.HomeStatistics,
                Odds = TennisDataBinding.OddsJson
            });
            match.Players.Add(new MatchPlayer
            {
                Id = Guid.NewGuid(),
                Side = "away",
                IsWinner = mapped.WinnerCode == 2,
                Statistics = mapped.AwayStatistics,
                Odds = TennisDataBinding.OddsJson
            });
            _db.Matches.Add(match);
            inserted++;
            pending++;
            if (pending < BatchSize)
            {
                continue;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            pending = 0;
        }

        if (pending > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }

        var stored = await _db.ImportFiles.FirstAsync(file => file.Id == fileRecord.Id, cancellationToken);
        stored.RowsRead = rowsRead;
        stored.MatchesInserted = inserted;
        stored.DuplicatesSkipped = duplicates;
        stored.InvalidRowsSkipped = invalid;
        stored.MissingStatisticCells = missing;
        await _db.SaveChangesAsync(cancellationToken);
        return new FileOutcome(rowsRead, inserted, duplicates, invalid, missing, plan.UnmappedColumns);
    }

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

    private static string BuildMessage(string downloadMessage, int fileCount, IReadOnlyList<string> unmappedColumns)
    {
        var files = fileCount == 0
            ? " No yyyy-atp-season.csv files for 2021-2026 were in Data/source."
            : " Unchanged files are skipped by SHA-256.";
        var seen = unmappedColumns.Count == 0
            ? " No extra CSV columns were seen."
            : " CSV columns that did not map, kept in RawRecord: " + string.Join(", ", unmappedColumns) + ".";
        var unpublished = string.Join(" ", TennisDataBinding.Unpublished.Select(field => field.Field + " (" + field.Reason + ")"));
        return downloadMessage
            + files
            + " Mapped: winner_code to Match.WinnerCode (1 = home, 2 = away); each home_ column to the home MatchPlayer statistics; each away_ column to the away MatchPlayer statistics; every column to Match.RawRecord."
            + seen
            + " Not guessed: "
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
        public int DuplicatesSkipped { get; set; }
        public int InvalidRowsSkipped { get; set; }
        public int MissingStatisticCells { get; set; }

        public YearImportCount ToResponse(int year) => new(
            year,
            RowsRead,
            MatchesInserted,
            0,
            DuplicatesSkipped,
            InvalidRowsSkipped,
            MissingStatisticCells);
    }

    private sealed record FileOutcome(
        int RowsRead,
        int MatchesInserted,
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
