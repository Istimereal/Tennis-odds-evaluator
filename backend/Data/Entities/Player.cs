namespace Tennis.Api.Data.Entities;

public class Player
{
    public Guid Id { get; set; }
    public string? SourceId { get; set; }
    public string Name { get; set; } = "";
    public List<MatchPlayer> MatchPlayers { get; set; } = new();
}
