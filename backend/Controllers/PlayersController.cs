using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Models;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/players")]
public class PlayersController : ControllerBase
{
    private readonly PlayerService _players;

    public PlayersController(PlayerService players)
    {
        _players = players;
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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var player = await _players.GetAsync(id, cancellationToken);
        return player is null ? NotFound("Player was not found.") : Ok(player);
    }
}
