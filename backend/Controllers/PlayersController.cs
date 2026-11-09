using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Models;
using Tennis.Api.Services;
using Tennis.Api.Statistics;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly PlayerService _players;
    private readonly PlayerStatisticsService _statistics;

    public PlayersController(PlayerService players, PlayerStatisticsService statistics)
    {
        _players = players;
        _statistics = statistics;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? name = null,
        CancellationToken cancellationToken = default)
    {
        var error = PageQuery.Validate(page, pageSize);
        if (error is not null)
        {
            return BadRequest(error);
        }

        return Ok(await _players.GetPageAsync(page, pageSize, name, cancellationToken));
    }

    [HttpGet("{id:guid}/matches")]
    public async Task<IActionResult> GetMatches(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PlayerService.DefaultHistoryPageSize,
        CancellationToken cancellationToken = default)
    {
        var error = PageQuery.Validate(page, pageSize);
        if (error is not null)
        {
            return BadRequest(error);
        }

        var history = await _players.GetMatchHistoryAsync(id, page, pageSize, cancellationToken);
        return history is null ? NotFound("Player was not found.") : Ok(history);
    }

    [HttpGet("{id:guid}/statistics")]
    public async Task<IActionResult> GetStatistics(
        Guid id,
        [FromQuery] DateOnly? asOfDate,
        [FromQuery] string[]? surface,
        [FromQuery] string? level,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? rankingGroup,
        [FromQuery] int? recent,
        [FromQuery] Guid? excludeMatchId,
        CancellationToken cancellationToken)
    {
        if (!PlayerStatisticsRequest.TryCreate(asOfDate, surface, level, from, to, rankingGroup, recent, excludeMatchId, out var request, out var error))
        {
            return BadRequest(error);
        }

        var report = await _statistics.GetAsync(id, request!, cancellationToken);
        return report is null ? NotFound("Player was not found.") : Ok(report);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var player = await _players.GetAsync(id, cancellationToken);
        return player is null ? NotFound("Player was not found.") : Ok(player);
    }
}
