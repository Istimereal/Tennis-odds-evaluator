namespace Tennis.Api.Data.Entities;

public class TournamentEdition
{
    public Guid Id { get; set; }
    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;
    public int Year { get; set; }
    public string? Surface { get; set; }
    public string? Level { get; set; }
    public string? Name { get; set; }
    public DateOnly? StartDate { get; set; }
    public List<Match> Matches { get; set; } = new();
}
