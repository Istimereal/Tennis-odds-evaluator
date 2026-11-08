namespace Tennis.Api.Data.Entities;

public class Tournament
{
    public Guid Id { get; set; }
    public string? SourceId { get; set; }
    public string Name { get; set; } = "";
    public List<TournamentEdition> Editions { get; set; } = new();
}
