using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class TournamentService
{
    private readonly AppDbContext _db;

    public TournamentService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PageResponse<TournamentResponse>> GetPageAsync(
        int page,
        int pageSize,
        string? surface,
        string? level,
        int? year,
        CancellationToken cancellationToken)
    {
        var query = _db.TournamentEditions.AsNoTracking().Include(edition => edition.Tournament).AsQueryable();
        if (!string.IsNullOrWhiteSpace(surface))
        {
            query = query.Where(edition => edition.Surface == surface);
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(edition => edition.Level == level);
        }

        if (year is int selectedYear)
        {
            query = query.Where(edition => edition.Year == selectedYear);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(edition => edition.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(edition => new TournamentResponse(
                edition.TournamentId,
                edition.Tournament.SourceId,
                edition.Tournament.Name,
                edition.Year,
                edition.Surface,
                edition.Level,
                edition.StartDate))
            .ToListAsync(cancellationToken);
        return new PageResponse<TournamentResponse>(items, page, pageSize, total);
    }
}
