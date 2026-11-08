namespace Tennis.Api.Import;

public sealed record DownloadResult(bool Automated, IReadOnlyList<string> Files, string Message);

public class TennisDataDownloadClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;

    public TennisDataDownloadClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _configuration = configuration;
    }

    public async Task<DownloadResult> TryDownloadAsync(
        IReadOnlyList<int> years,
        string mode,
        string destinationDirectory,
        CancellationToken cancellationToken)
    {
        var template = _configuration["TennisData:SeasonFileUrlTemplate"];
        if (string.IsNullOrWhiteSpace(template))
        {
            return new DownloadResult(
                false,
                [],
                "HTTP download is unavailable. https://tennisdata.app/downloads/ requires a Cloudflare human check, and sitemap.xml does not publish a direct CSV URL. No URL was guessed.");
        }

        if (!template.Contains("{year}", StringComparison.Ordinal))
        {
            return new DownloadResult(false, [], "TennisData:SeasonFileUrlTemplate must contain {year}. No download was attempted.");
        }

        Directory.CreateDirectory(destinationDirectory);
        var saved = new List<string>();
        foreach (var year in years.Distinct().Order())
        {
            var destination = Path.Combine(destinationDirectory, $"{year}-atp-season.csv");
            if (mode == ImportModes.Incremental && File.Exists(destination))
            {
                continue;
            }

            var url = template.Replace("{year}", year.ToString(), StringComparison.Ordinal);
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new DownloadResult(
                    false,
                    saved,
                    $"Download for {year} returned HTTP {(int)response.StatusCode}. The URL came from configuration, not a guessed path.");
            }

            var partial = destination + ".partial";
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var output = File.Create(partial))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            File.Move(partial, destination, overwrite: true);
            saved.Add(destination);
        }

        return new DownloadResult(true, saved, "Season files were downloaded with HttpClient from the configured URL template.");
    }
}
