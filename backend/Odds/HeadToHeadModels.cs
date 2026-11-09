namespace Tennis.Api.Odds;

public static class OddsStatuses
{
    public const string Ok = "OK";
    public const string NoHeadToHead = "NO_HEAD_TO_HEAD";
    public const string InsufficientData = "INSUFFICIENT_DATA";
    public const string InvalidPlayerSelection = "INVALID_PLAYER_SELECTION";
    public const string PlayerNotFound = "PLAYER_NOT_FOUND";
    public const string DatabaseError = "DATABASE_ERROR";
    public const string CalculationError = "CALCULATION_ERROR";
    public const string HeadToHeadMethod = "HEAD_TO_HEAD";
    public const string NoMatchingPossible = "No matching possible";
}

public sealed record PlayerSnapshot(
    Guid Id,
    string Name,
    string? SourceId,
    string? Nationality,
    int MatchCount,
    int Wins,
    int Losses);

public sealed record MatchContext(
    string? Tournament,
    DateTimeOffset? StartsAt,
    string? Round,
    string? Surface,
    string? Venue);

public sealed record SourceMatch(
    Guid MatchId,
    string SourceKey,
    short? WinnerCode,
    string? Status,
    string? StatusExtra,
    Guid? HomePlayerId,
    Guid? AwayPlayerId,
    bool? HomeIsWinner,
    bool? AwayIsWinner,
    DateOnly? PlayedOn,
    string? TournamentName,
    string? Surface,
    string? Round,
    string? Score);

public sealed record CountedMeeting(
    Guid MatchId,
    string SourceKey,
    Guid WinnerId,
    DateOnly? PlayedOn,
    string? TournamentName,
    string? Surface,
    string? Round,
    string? Score);

public sealed record HeadToHeadResult(
    string Status,
    string Message,
    int SharedMatchCount,
    int CountedMatchCount,
    int PlayerAWins,
    int PlayerBWins,
    decimal? PlayerAProbability,
    decimal? PlayerBProbability,
    decimal? PlayerADecimalOdds,
    decimal? PlayerBDecimalOdds,
    IReadOnlyList<CountedMeeting> Meetings);

public sealed record OddsEvaluationRequest(
    Guid PlayerAId,
    string PlayerAName,
    Guid PlayerBId,
    string PlayerBName,
    MatchContext? MatchContext);
