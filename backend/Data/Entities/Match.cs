namespace Tennis.Api.Data.Entities;

public class Match
{
    public Guid Id { get; set; }
    public string SourceKey { get; set; } = "";
    public int SourceYear { get; set; }
    public string SeasonFileName { get; set; } = "";
    public short? WinnerCode { get; set; }
    public DateOnly? PlayedOn { get; set; }
    public string RawRecord { get; set; } = "{}";
    public Guid? TournamentEditionId { get; set; }
    public TournamentEdition? TournamentEdition { get; set; }
    public Guid ImportFileId { get; set; }
    public ImportFileRecord ImportFile { get; set; } = null!;
    public List<MatchPlayer> Players { get; set; } = new();
}
