using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/player-comparison")]
public class PlayerComparisonController : ControllerBase
{
    private readonly PlayerComparisonService _comparison;

    public PlayerComparisonController(PlayerComparisonService comparison)
    {
        _comparison = comparison;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] Guid playerAId,
        [FromQuery] Guid playerBId,
        CancellationToken cancellationToken)
    {
        if (playerAId == Guid.Empty || playerBId == Guid.Empty)
        {
            return BadRequest("playerAId and playerBId are required.");
        }

        return Ok(await _comparison.CompareAsync(playerAId, playerBId, cancellationToken));
    }
}
