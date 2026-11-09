namespace Tennis.Api.Data.Entities;

// Parsed numbers for one player in one match. MatchPlayer.Statistics keeps the original cell text.
public class MatchPlayerStatistics
{
    public Guid Id { get; set; }
    public Guid MatchPlayerId { get; set; }
    public MatchPlayer MatchPlayer { get; set; } = null!;

    public int? Aces { get; set; }
    public int? DoubleFaults { get; set; }

    // Percentage points copied from *_perc cells. 80 means 80%.
    public decimal? ServicePointsWonPercent { get; set; }
    public decimal? ReturnPointsWonPercent { get; set; }
    public decimal? BreakPointsWonPercent { get; set; }
    public decimal? BreakPointsSavedPercent { get; set; }

    // Reserved for a later file. The importer writes null until those columns are mapped.
    public decimal? FirstServePercent { get; set; }
    public decimal? FirstServePointsWonPercent { get; set; }
    public decimal? SecondServePointsWonPercent { get; set; }
    public int? BreakPointsFaced { get; set; }
    public int? BreakPointsSaved { get; set; }
    public int? BreakPointChances { get; set; }
    public int? BreakPointsConverted { get; set; }
    public int? Winners { get; set; }
    public int? UnforcedErrors { get; set; }
    public int? NetPointsWon { get; set; }
    public int? TotalPointsWon { get; set; }
}
