using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Import;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class ImportHistoryService
{
    private readonly AppDbContext _db;

    public ImportHistoryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PageResponse<ImportBatchResponse>> GetPageAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = _db.ImportBatches.AsNoTracking();
        var total = await query.CountAsync(cancellationToken);
        var batches = await query
            .OrderByDescending(batch => batch.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(batch => batch.Files)
            .ToListAsync(cancellationToken);

        var items = batches.Select(batch => new ImportBatchResponse(
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
            batch.Error,
            batch.Files
                .OrderBy(file => file.SourceYear)
                .Select(file => new ImportFileResponse(
                    file.Id,
                    file.SourceYear,
                    file.OriginalFileName,
                    file.Sha256,
                    file.Status,
                    file.RowsRead,
                    file.MatchesInserted,
                    file.DuplicatesSkipped,
                    file.InvalidRowsSkipped,
                    file.MissingStatisticCells,
                    TennisDataBinding.ReadColumns(file.UnmappedColumns),
                    file.Error,
                    file.ImportedAt))
                .ToArray())).ToArray();
        return new PageResponse<ImportBatchResponse>(items, page, pageSize, total);
    }
}
