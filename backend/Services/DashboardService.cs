using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Import;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class DashboardService
{
    private readonly AppDbContext _db;

    public DashboardService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardResponse> GetAsync(CancellationToken cancellationToken)
    {
        var players = await _db.Players.CountAsync(cancellationToken);
        var matches = await _db.Matches.CountAsync(cancellationToken);
        var tournaments = await _db.Tournaments.CountAsync(cancellationToken);
        var importedSeasons = await _db.ImportFiles
            .Where(file => file.Status == "imported" && file.SourceYear != null)
            .Select(file => file.SourceYear)
            .Distinct()
            .CountAsync(cancellationToken);
        var latest = await LatestAsync(null, cancellationToken);
        var latestSuccess = await LatestAsync(successful: true, cancellationToken);
        var failedFiles = await _db.ImportFiles.CountAsync(file => file.Status == "rejected", cancellationToken);
        var batchesWithErrors = await _db.ImportBatches.CountAsync(batch => batch.Error != null, cancellationToken);
        var yearRows = await _db.Matches
            .AsNoTracking()
            .GroupBy(match => match.SourceYear)
            .Select(group => new { Year = group.Key, Count = group.Count() })
            .OrderBy(row => row.Year)
            .ToListAsync(cancellationToken);
        var byYear = yearRows.Select(row => new YearCount(row.Year, row.Count)).ToArray();
        var bySurface = await NamedCountsAsync(surface: true, cancellationToken);
        var byLevel = await NamedCountsAsync(surface: false, cancellationToken);
        var missingCells = await _db.ImportFiles
            .Where(file => file.Status == "imported")
            .SumAsync(file => (int?)file.MissingStatisticCells, cancellationToken) ?? 0;

        return new DashboardResponse(
            players,
            matches,
            tournaments,
            importedSeasons,
            latest,
            latestSuccess,
            failedFiles,
            batchesWithErrors,
            new CompletenessResponse(
                matches,
                await _db.Matches.CountAsync(match => match.WinnerCode != null, cancellationToken),
                await _db.Matches.CountAsync(match => match.PlayedOn != null, cancellationToken),
                await _db.Matches.CountAsync(match => match.TournamentEdition != null && match.TournamentEdition.StartDate != null, cancellationToken),
                await _db.Matches.CountAsync(match => match.TournamentEdition != null && match.TournamentEdition.Surface != null, cancellationToken),
                await _db.Matches.CountAsync(match => match.TournamentEdition != null && match.TournamentEdition.Level != null, cancellationToken),
                missingCells),
            byYear,
            bySurface,
            byLevel);
    }

    public async Task<CoverageResponse> GetCoverageAsync(CancellationToken cancellationToken)
    {
        var files = await _db.ImportFiles
            .AsNoTracking()
            .Where(file => file.Status == "imported" && file.SourceYear != null)
            .Select(file => new
            {
                file.SourceYear,
                file.RowsRead,
                file.MatchesInserted,
                file.DuplicatesSkipped,
                file.InvalidRowsSkipped,
                file.MissingStatisticCells,
                file.UnmappedColumns
            })
            .ToListAsync(cancellationToken);

        var seasons = TennisSeasons.Supported.Select(year =>
        {
            var rows = files.Where(file => file.SourceYear == year).ToArray();
            var unmapped = rows
                .SelectMany(file => TennisDataBinding.ReadColumns(file.UnmappedColumns))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            return new SeasonCoverage(
                year,
                rows.Length,
                rows.Sum(file => file.RowsRead),
                rows.Sum(file => file.MatchesInserted),
                rows.Sum(file => file.DuplicatesSkipped),
                rows.Sum(file => file.InvalidRowsSkipped),
                rows.Sum(file => file.MissingStatisticCells),
                unmapped);
        }).ToArray();

        return new CoverageResponse(seasons, TennisDataBinding.Unpublished);
    }

    private async Task<ImportStatusResponse?> LatestAsync(bool? successful, CancellationToken cancellationToken)
    {
        var query = _db.ImportBatches.AsNoTracking();
        if (successful == true)
        {
            query = query.Where(batch => batch.Error == null && batch.MatchesInserted > 0);
        }

        return await query
            .OrderByDescending(batch => batch.StartedAt)
            .Select(batch => new ImportStatusResponse(
                batch.Id,
                batch.Mode,
                batch.StartedAt,
                batch.FinishedAt,
                batch.FilesProcessed,
                batch.MatchesInserted,
                batch.InvalidRowsSkipped,
                batch.Error))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<NamedCount>> NamedCountsAsync(bool surface, CancellationToken cancellationToken)
    {
        var grouped = surface
            ? await _db.Matches.AsNoTracking()
                .Where(match => match.TournamentEdition != null && match.TournamentEdition.Surface != null)
                .GroupBy(match => match.TournamentEdition!.Surface!)
                .Select(group => new { Name = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken)
            : await _db.Matches.AsNoTracking()
                .Where(match => match.TournamentEdition != null && match.TournamentEdition.Level != null)
                .GroupBy(match => match.TournamentEdition!.Level!)
                .Select(group => new { Name = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
        var mapped = grouped.Select(row => new NamedCount(row.Name, row.Count)).ToList();
        var missing = surface
            ? await _db.Matches.CountAsync(match => match.TournamentEdition == null || match.TournamentEdition.Surface == null, cancellationToken)
            : await _db.Matches.CountAsync(match => match.TournamentEdition == null || match.TournamentEdition.Level == null, cancellationToken);
        if (missing > 0 && await _db.Matches.AnyAsync(cancellationToken))
        {
            mapped.Add(new NamedCount("unmapped", missing));
        }

        return mapped.OrderBy(row => row.Name, StringComparer.Ordinal).ToArray();
    }
}
