using System.Text.RegularExpressions;

namespace Tennis.Api.Import;

public class TennisSeasonFileSource
{
    private static readonly Regex FileNamePattern = new(@"^(?<year>\d{4})-atp-season\.csv$", RegexOptions.CultureInvariant);

    private readonly IWebHostEnvironment _environment;

    public TennisSeasonFileSource(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string DirectoryPath => Path.Combine(_environment.ContentRootPath, "Data", "source");

    public IReadOnlyList<SeasonFile> Find(IReadOnlyList<int> years)
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return [];
        }

        var wanted = years.ToHashSet();
        var files = new List<SeasonFile>();
        foreach (var path in Directory.EnumerateFiles(DirectoryPath, "*-atp-season.csv"))
        {
            var fileName = Path.GetFileName(path);
            if (!FileNamePattern.IsMatch(fileName))
            {
                continue;
            }

            var year = int.Parse(FileNamePattern.Match(fileName).Groups["year"].Value);
            if (wanted.Contains(year))
            {
                files.Add(new SeasonFile(path, fileName, year));
            }
        }

        return files.OrderBy(file => file.Year).ThenBy(file => file.FileName, StringComparer.Ordinal).ToArray();
    }

    public string SaveUpload(string originalFileName, Stream content)
    {
        var safeName = Path.GetFileName(originalFileName);
        var match = FileNamePattern.Match(safeName);
        if (!match.Success)
        {
            throw new InvalidDataException("Upload the provider file as yyyy-atp-season.csv for a year from 2021 through 2026.");
        }

        var year = int.Parse(match.Groups["year"].Value);
        if (year == 2020)
        {
            throw new InvalidDataException("2020 is a coverage gap for TennisData.app and cannot be uploaded.");
        }

        if (!TennisSeasons.Supported.Contains(year))
        {
            throw new InvalidDataException("Only ATP seasons 2021-2026 can be uploaded.");
        }

        Directory.CreateDirectory(DirectoryPath);
        var destination = Path.Combine(DirectoryPath, safeName);
        using var output = File.Create(destination);
        content.CopyTo(output);
        return destination;
    }
}
