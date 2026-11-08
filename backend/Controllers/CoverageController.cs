using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/coverage")]
public class CoverageController : ControllerBase
{
    private readonly DashboardService _dashboard;

    public CoverageController(DashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        return Ok(await _dashboard.GetCoverageAsync(cancellationToken));
    }
}
