using Microsoft.AspNetCore.Mvc;
using Tennis.Api.Import;
using Tennis.Api.Models;
using Tennis.Api.Services;

namespace Tennis.Api.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController : ControllerBase
{
    private readonly TennisDataImportService _import;
    private readonly TennisSeasonFileSource _files;

    public ImportController(TennisDataImportService import, TennisSeasonFileSource files)
    {
        _import = import;
        _files = files;
    }

    [HttpPost]
    public async Task<IActionResult> Import([FromBody] ImportRequest? request, CancellationToken cancellationToken)
    {
        return await RunAsync(request, download: true, cancellationToken);
    }

    [HttpPost("local")]
    public async Task<IActionResult> ImportLocal([FromBody] ImportRequest? request, CancellationToken cancellationToken)
    {
        return await RunAsync(request, download: false, cancellationToken);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(80_000_000)]
    public async Task<IActionResult> Upload([FromForm] List<IFormFile> files, [FromQuery] string? mode, CancellationToken cancellationToken)
    {
        if (!ImportModes.TryNormalize(mode, out _))
        {
            return BadRequest("Mode must be backfill, incremental, or refresh.");
        }

        if (files.Count == 0)
        {
            return BadRequest("Choose one or more yyyy-atp-season.csv files.");
        }

        if (files.Count > TennisSeasons.Supported.Length)
        {
            return BadRequest("Upload at most one file for each ATP season from 2021 through 2026.");
        }

        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                return BadRequest(Path.GetFileName(file.FileName) + " is empty.");
            }

            if (file.Length > 40_000_000)
            {
                return BadRequest(Path.GetFileName(file.FileName) + " is larger than 40 MB.");
            }

            await using var stream = file.OpenReadStream();
            try
            {
                _files.SaveUpload(file.FileName, stream);
            }
            catch (InvalidDataException exception)
            {
                return BadRequest(exception.Message);
            }
        }

        return await RunAsync(new ImportRequest(mode, null), download: false, cancellationToken);
    }

    private async Task<IActionResult> RunAsync(ImportRequest? request, bool download, CancellationToken cancellationToken)
    {
        var outcome = await _import.ImportAsync(request?.Mode, request?.Years, download, cancellationToken);
        if (outcome.Error is not null)
        {
            return StatusCode(outcome.StatusCode, outcome.Error);
        }

        return Ok(outcome.Summary);
    }
}
