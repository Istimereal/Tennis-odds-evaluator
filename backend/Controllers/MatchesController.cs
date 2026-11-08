using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Models;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/matches")]
public class MatchesController : ControllerBase
{
    private readonly MatchService _matches;

    public MatchesController(MatchService matches)
    {
        _matches = matches;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? playerId = null,
        [FromQuery] Guid? tournamentId = null,
        [FromQuery] string? surface = null,
        [FromQuery] string? level = null,
        [FromQuery] int? year = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var error = PageQuery.Validate(page, pageSize);
        if (error is not null)
        {
            return BadRequest(error);
        }

        return Ok(await _matches.GetPageAsync(page, pageSize, playerId, tournamentId, surface, level, year, from, to, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var match = await _matches.GetAsync(id, cancellationToken);
        return match is null ? NotFound("Match was not found.") : Ok(match);
    }
}
