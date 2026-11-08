using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Data.Entities;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class MatchService
{
    private readonly AppDbContext _db;

    public MatchService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PageResponse<MatchResponse>> GetPageAsync(
        int page,
        int pageSize,
        Guid? playerId,
        Guid? tournamentId,
        string? surface,
        string? level,
        int? year,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = Filtered(playerId, tournamentId, surface, level, year, from, to);
        var total = await query.CountAsync(cancellationToken);
        var ids = await query
            .OrderByDescending(match => match.SourceYear)
            .ThenBy(match => match.SourceKey)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(match => match.Id)
            .ToListAsync(cancellationToken);

        // Include loads the tournament edition and the match players for this page.
        var matches = await _db.Matches
            .AsNoTracking()
            .Include(match => match.TournamentEdition)
            .ThenInclude(edition => edition!.Tournament)
            .Include(match => match.Players)
            .ThenInclude(row => row.Player)
            .Where(match => ids.Contains(match.Id))
            .ToListAsync(cancellationToken);

        var ordered = ids.Select(id => matches.First(match => match.Id == id)).Select(match => ToResponse(match, includeRaw: false)).ToArray();
        return new PageResponse<MatchResponse>(ordered, page, pageSize, total);
    }

    public async Task<MatchResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var match = await _db.Matches
            .AsNoTracking()
            .Include(item => item.TournamentEdition)
            .ThenInclude(edition => edition!.Tournament)
            .Include(item => item.Players)
            .ThenInclude(row => row.Player)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        return match is null ? null : ToResponse(match, includeRaw: true);
    }

    private IQueryable<Match> Filtered(
        Guid? playerId,
        Guid? tournamentId,
        string? surface,
        string? level,
        int? year,
        DateOnly? from,
        DateOnly? to)
    {
        var query = _db.Matches.AsNoTracking().AsQueryable();
        if (playerId is Guid selectedPlayer)
        {
            query = query.Where(match => match.Players.Any(row => row.PlayerId == selectedPlayer));
        }

        if (tournamentId is Guid selectedTournament)
        {
            query = query.Where(match => match.TournamentEdition != null && match.TournamentEdition.TournamentId == selectedTournament);
        }

        if (!string.IsNullOrWhiteSpace(surface))
        {
            query = query.Where(match => match.TournamentEdition != null && match.TournamentEdition.Surface == surface);
        }

        if (!string.IsNullOrWhiteSpace(level))
        {
            query = query.Where(match => match.TournamentEdition != null && match.TournamentEdition.Level == level);
        }

        if (year is int selectedYear)
        {
            query = query.Where(match => match.SourceYear == selectedYear);
        }

        if (from is DateOnly start)
        {
            query = query.Where(match => match.PlayedOn != null && match.PlayedOn >= start);
        }

        if (to is DateOnly end)
        {
            query = query.Where(match => match.PlayedOn != null && match.PlayedOn <= end);
        }

        return query;
    }

    private static MatchResponse ToResponse(Match match, bool includeRaw)
    {
        var edition = match.TournamentEdition;
        return new MatchResponse(
            match.Id,
            match.SourceKey,
            match.SourceYear,
            match.WinnerCode,
            match.PlayedOn,
            edition?.TournamentId,
            edition?.Tournament.Name ?? edition?.Name,
            edition?.Surface,
            edition?.Level,
            match.Players
                .OrderBy(row => row.Side, StringComparer.Ordinal)
                .Select(row => new MatchPlayerResponse(
                    row.PlayerId,
                    row.Player?.SourceId,
                    row.Player?.Name,
                    row.Side,
                    row.IsWinner,
                    row.Ranking,
                    row.Statistics,
                    row.Odds))
                .ToArray(),
            includeRaw ? match.RawRecord : null,
            null,
            null,
            StatCompleteness(match));
    }

    private static double? StatCompleteness(Match match)
    {
        var cells = 0;
        var filled = 0;
        foreach (var row in match.Players)
        {
            if (string.IsNullOrWhiteSpace(row.Statistics))
            {
                continue;
            }

            using var document = JsonDocument.Parse(row.Statistics);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                cells++;
                if (property.Value.ValueKind != JsonValueKind.Null)
                {
                    filled++;
                }
            }
        }

        return cells == 0 ? null : (double)filled / cells;
    }
}
