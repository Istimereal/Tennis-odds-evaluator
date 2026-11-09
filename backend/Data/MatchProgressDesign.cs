namespace Tennis.Api.Data;

// The season CSV has no game or point rows. These shapes are the later model.
// They are not EF entities, and the database has no game or point tables.
public static class MatchGameTypes
{
    public const string Regular = "regular";
    public const string Tiebreak = "tiebreak";
    public const string MatchTiebreak = "match-tiebreak";
}

public static class ServeStatuses
{
    public const string First = "first";
    public const string Second = "second";
}

public sealed record MatchGameDesign(
    int SetNumber,
    int Sequence,
    string? ServerSide,
    string? WinnerSide,
    string GameType);

public sealed record MatchPointDesign(
    int Sequence,
    string? ServerSide,
    string? ReturnerSide,
    string? WinnerSide,
    string? ServeStatus,
    string? Outcome,
    string? Score);

public sealed record MatchConditionsDesign(
    int? DurationMinutes,
    DateTimeOffset? ActualStart,
    DateTimeOffset? CompletedAt,
    string? Roof,
    string? Venue,
    string? Interruptions);
