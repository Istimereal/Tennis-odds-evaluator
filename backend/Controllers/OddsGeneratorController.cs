using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/odds-generator")]
public class OddsGeneratorController : ControllerBase
{
    private readonly OddsGeneratorService _odds;

    public OddsGeneratorController(OddsGeneratorService odds)
    {
        _odds = odds;
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

        return Ok(await _odds.GenerateAsync(playerAId, playerBId, null, cancellationToken));
    }
}
