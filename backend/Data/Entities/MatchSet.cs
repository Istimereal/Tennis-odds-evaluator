namespace Tennis.Api.Data.Entities;

// Games in one set. Home and away are the MatchPlayer rows with those sides.
public class MatchSet
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public Match Match { get; set; } = null!;
    public int SetNumber { get; set; }
    public int? HomeGames { get; set; }
    public int? AwayGames { get; set; }
    public int? HomeTiebreak { get; set; }
    public int? AwayTiebreak { get; set; }
}
