using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Models;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/tournaments")]
public class TournamentsController : ControllerBase
{
    private readonly TournamentService _tournaments;

    public TournamentsController(TournamentService tournaments)
    {
        _tournaments = tournaments;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? surface = null,
        [FromQuery] string? level = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var error = PageQuery.Validate(page, pageSize);
        if (error is not null)
        {
            return BadRequest(error);
        }

        return Ok(await _tournaments.GetPageAsync(page, pageSize, surface, level, year, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tournament = await _tournaments.GetAsync(id, cancellationToken);
        return tournament is null ? NotFound("Tournament was not found.") : Ok(tournament);
    }
}
