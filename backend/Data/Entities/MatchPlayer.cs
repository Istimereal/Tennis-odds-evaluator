namespace Tennis.Api.Data.Entities;

public class MatchPlayer
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public Guid? PlayerId { get; set; }
    public Player? Player { get; set; }
    public string Side { get; set; } = "";
    public bool? IsWinner { get; set; }
    public int? Ranking { get; set; }
    public string Statistics { get; set; } = "{}";
    public string Odds { get; set; } = "{}";
}
