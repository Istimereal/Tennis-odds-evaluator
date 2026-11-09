using System.Data.Common;
using Tennis.Api.Models;
using Tennis.Api.Odds;

namespace Tennis.Api.Services;

public class OddsGeneratorService
{
    private readonly IPlayerDirectory _players;
    private readonly IReadOnlyList<IOddsMethod> _methods;

    public OddsGeneratorService(IPlayerDirectory players, IEnumerable<IOddsMethod> methods)
    {
        _players = players;
        _methods = methods.ToArray();
    }

    public async Task<OddsGeneratorResponse> GenerateAsync(
        Guid playerAId,
        Guid playerBId,
        MatchContext? matchContext,
        CancellationToken cancellationToken)
    {
        if (playerAId == playerBId)
        {
            return Build(
                playerAId,
                null,
                playerBId,
                null,
                OddsStatuses.HeadToHeadMethod,
                OddsStatuses.InvalidPlayerSelection,
                "Select two different players.",
                0,
                null,
                null,
                null,
                null,
                matchContext,
                []);
        }

        PlayerSnapshot? playerA;
        PlayerSnapshot? playerB;
        try
        {
            playerA = await _players.FindAsync(playerAId, cancellationToken);
            playerB = await _players.FindAsync(playerBId, cancellationToken);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException)
        {
            return Build(
                playerAId,
                null,
                playerBId,
                null,
                OddsStatuses.HeadToHeadMethod,
                OddsStatuses.DatabaseError,
                "The players could not be loaded.",
                0,
                null,
                null,
                null,
                null,
                matchContext,
                []);
        }

        if (playerA is null || playerB is null)
        {
            return Build(
                playerAId,
                playerA?.Name,
                playerBId,
                playerB?.Name,
                OddsStatuses.HeadToHeadMethod,
                OddsStatuses.PlayerNotFound,
                "Player was not found.",
                0,
                null,
                null,
                null,
                null,
                matchContext,
                []);
        }

        var request = new OddsEvaluationRequest(playerA.Id, playerA.Name, playerB.Id, playerB.Name, matchContext);
        var estimates = new List<OddsMethodEstimate>();
        foreach (var method in _methods)
        {
            estimates.Add(await method.EvaluateAsync(request, cancellationToken));
        }

        var primary = estimates.FirstOrDefault(estimate => estimate.Method == OddsStatuses.HeadToHeadMethod) ?? estimates.FirstOrDefault();
        if (primary is null)
        {
            return Build(
                playerA.Id,
                playerA.Name,
                playerB.Id,
                playerB.Name,
                OddsStatuses.HeadToHeadMethod,
                OddsStatuses.CalculationError,
                "No odds method is registered.",
                0,
                null,
                null,
                null,
                null,
                matchContext,
                []);
        }

        return Build(
            playerA.Id,
            playerA.Name,
            playerB.Id,
            playerB.Name,
            primary.Method,
            primary.Status,
            primary.Message,
            primary.HeadToHeadMatchCount,
            primary.PlayerAProbability,
            primary.PlayerBProbability,
            primary.PlayerADecimalOdds,
            primary.PlayerBDecimalOdds,
            matchContext,
            estimates);
    }

    private static OddsGeneratorResponse Build(
        Guid playerAId,
        string? playerAName,
        Guid playerBId,
        string? playerBName,
        string method,
        string status,
        string message,
        int headToHeadMatchCount,
        decimal? playerAProbability,
        decimal? playerBProbability,
        decimal? playerADecimalOdds,
        decimal? playerBDecimalOdds,
        MatchContext? matchContext,
        IReadOnlyList<OddsMethodEstimate> methods)
    {
        return new OddsGeneratorResponse(
            playerAId,
            playerAName,
            playerBId,
            playerBName,
            playerAProbability,
            playerBProbability,
            playerADecimalOdds,
            playerBDecimalOdds,
            method,
            headToHeadMatchCount,
            status,
            message,
            matchContext is null
                ? null
                : new MatchContextResponse(
                    matchContext.Tournament,
                    matchContext.StartsAt,
                    matchContext.Round,
                    matchContext.Surface,
                    matchContext.Venue),
            methods.Select(estimate => new OddsMethodResponse(
                estimate.Method,
                estimate.Status,
                estimate.Message,
                estimate.HeadToHeadMatchCount,
                estimate.PlayerAProbability,
                estimate.PlayerBProbability,
                estimate.PlayerADecimalOdds,
                estimate.PlayerBDecimalOdds)).ToArray(),
            null);
    }
}
