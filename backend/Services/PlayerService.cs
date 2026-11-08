using Microsoft.EntityFrameworkCore;
using Tennis.Api.Data;
using Tennis.Api.Models;

namespace Tennis.Api.Services;

public class PlayerService
{
    private readonly AppDbContext _db;

    public PlayerService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PageResponse<PlayerResponse>> GetPageAsync(int page, int pageSize, string? name, CancellationToken cancellationToken)
    {
        var query = _db.Players.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(name))
        {
            query = query.Where(player => player.Name.Contains(name));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(player => player.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(player => new PlayerResponse(player.Id, player.SourceId, player.Name))
            .ToListAsync(cancellationToken);
        return new PageResponse<PlayerResponse>(items, page, pageSize, total);
    }

    public async Task<PlayerResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _db.Players
            .AsNoTracking()
            .Where(player => player.Id == id)
            .Select(player => new PlayerResponse(player.Id, player.SourceId, player.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
