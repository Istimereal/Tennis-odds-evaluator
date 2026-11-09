namespace Tennis.Api.Models;

public record MatchContextResponse(
    string? Tournament,
    DateTimeOffset? StartsAt,
    string? Round,
    string? Surface,
    string? Venue);

public record OddsMethodResponse(
    string Method,
    string Status,
    string Message,
    int HeadToHeadMatchCount,
    decimal? PlayerAProbability,
    decimal? PlayerBProbability,
    decimal? PlayerADecimalOdds,
    decimal? PlayerBDecimalOdds);

public record CombinedOddsResponse(
    string Status,
    string Message,
    decimal? PlayerAProbability,
    decimal? PlayerBProbability,
    decimal? PlayerADecimalOdds,
    decimal? PlayerBDecimalOdds);

public record OddsGeneratorResponse(
    Guid PlayerAId,
    string? PlayerAName,
    Guid PlayerBId,
    string? PlayerBName,
    decimal? PlayerAProbability,
    decimal? PlayerBProbability,
    decimal? PlayerADecimalOdds,
    decimal? PlayerBDecimalOdds,
    string Method,
    int HeadToHeadMatchCount,
    string Status,
    string Message,
    MatchContextResponse? MatchContext,
    IReadOnlyList<OddsMethodResponse> Methods,
    CombinedOddsResponse? Combined);

public record ComparisonPlayerResponse(
    Guid Id,
    string Name,
    string? SourceId,
    string? Nationality,
    int MatchCount,
    int Wins,
    int Losses);

public record HeadToHeadMeetingResponse(
    Guid MatchId,
    string SourceKey,
    DateOnly? Date,
    string? Tournament,
    string? Surface,
    string? Round,
    Guid? WinnerId,
    string? WinnerName,
    string? Score,
    string? Venue,
    string? Time);

public record PlayerComparisonResponse(
    string Status,
    string Message,
    ComparisonPlayerResponse? PlayerA,
    ComparisonPlayerResponse? PlayerB,
    int TotalMatches,
    int PlayerAWins,
    int PlayerBWins,
    decimal? PlayerAWinPercentage,
    decimal? PlayerBWinPercentage,
    IReadOnlyList<HeadToHeadMeetingResponse> Meetings,
    OddsGeneratorResponse Odds);
