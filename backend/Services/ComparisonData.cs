using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Import;
using Tennis.Api.Odds;

namespace Tennis.Api.Services;

public sealed class EfPlayerDirectory : IPlayerDirectory
{
    private readonly AppDbContext _db;

    public EfPlayerDirectory(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PlayerSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var player = await _db.Players
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.SourceId,
                MatchCount = item.MatchPlayers.Count(),
                Wins = item.MatchPlayers.Count(row => row.IsWinner == true),
                Losses = item.MatchPlayers.Count(row => row.IsWinner == false)
            })
            .FirstOrDefaultAsync(cancellationToken);
        return player is null
            ? null
            : new PlayerSnapshot(player.Id, player.Name, player.SourceId, null, player.MatchCount, player.Wins, player.Losses);
    }
}

public sealed class EfHeadToHeadMatchReader : IHeadToHeadMatchReader
{
    private readonly AppDbContext _db;

    public EfHeadToHeadMatchReader(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<SourceMatch>> LoadPairAsync(Guid playerAId, Guid playerBId, CancellationToken cancellationToken)
    {
        // Completed meetings only. MatchPlayerStatistics stay out of this query so a match's own numbers are not a pre-match odds input.
        var matches = await _db.Matches
            .AsNoTracking()
            .Include(match => match.Players)
            .Include(match => match.TournamentEdition)
            .ThenInclude(edition => edition!.Tournament)
            .Where(match =>
                match.Players.Any(row => row.PlayerId == playerAId) &&
                match.Players.Any(row => row.PlayerId == playerBId))
            .ToListAsync(cancellationToken);

        return matches.Select(match =>
        {
            var home = match.Players.FirstOrDefault(row => row.Side == "home");
            var away = match.Players.FirstOrDefault(row => row.Side == "away");
            var status = ReadText(match.RawRecord, "status");
            var statusExtra = ReadText(match.RawRecord, "status_extra");
            return new SourceMatch(
                match.Id,
                match.SourceKey,
                match.WinnerCode,
                status,
                statusExtra,
                home?.PlayerId,
                away?.PlayerId,
                home?.IsWinner,
                away?.IsWinner,
                match.PlayedOn,
                match.TournamentEdition?.Tournament.Name ?? match.TournamentEdition?.Name,
                match.TournamentEdition?.Surface,
                TennisDataRowMapper.ReadRound(match.RawRecord),
                TennisDataRowMapper.ReadScore(match.RawRecord));
        }).ToArray();
    }

    private static string? ReadText(string rawJson, string name)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!document.RootElement.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
