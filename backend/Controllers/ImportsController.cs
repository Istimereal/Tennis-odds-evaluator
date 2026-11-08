using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Models;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController : ControllerBase
{
    private readonly ImportHistoryService _history;

    public ImportsController(ImportHistoryService history)
    {
        _history = history;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var error = PageQuery.Validate(page, pageSize);
        if (error is not null)
        {
            return BadRequest(error);
        }

        return Ok(await _history.GetPageAsync(page, pageSize, cancellationToken));
    }
}
