using System.Data.Common;
using Tennis.Api.Models;
using Tennis.Api.Odds;

namespace Tennis.Api.Services;

public class PlayerComparisonService
{
    private readonly IPlayerDirectory _players;
    private readonly IHeadToHeadMatchReader _matches;
    private readonly HeadToHeadCalculationService _calculation;
    private readonly OddsGeneratorService _odds;

    public PlayerComparisonService(
        IPlayerDirectory players,
        IHeadToHeadMatchReader matches,
        HeadToHeadCalculationService calculation,
        OddsGeneratorService odds)
    {
        _players = players;
        _matches = matches;
        _calculation = calculation;
        _odds = odds;
    }

    public async Task<PlayerComparisonResponse> CompareAsync(Guid playerAId, Guid playerBId, CancellationToken cancellationToken)
    {
        var odds = await _odds.GenerateAsync(playerAId, playerBId, null, cancellationToken);
        if (odds.Status is OddsStatuses.InvalidPlayerSelection or OddsStatuses.PlayerNotFound or OddsStatuses.DatabaseError)
        {
            return new PlayerComparisonResponse(
                odds.Status,
                odds.Message,
                await TryPlayerAsync(playerAId, cancellationToken),
                playerAId == playerBId ? null : await TryPlayerAsync(playerBId, cancellationToken),
                0,
                0,
                0,
                null,
                null,
                [],
                odds);
        }

        try
        {
            var playerA = await _players.FindAsync(playerAId, cancellationToken);
            var playerB = await _players.FindAsync(playerBId, cancellationToken);
            var source = await _matches.LoadPairAsync(playerAId, playerBId, cancellationToken);
            var result = _calculation.Calculate(playerAId, playerBId, source);
            var names = new Dictionary<Guid, string>();
            if (playerA is not null)
            {
                names[playerA.Id] = playerA.Name;
            }

            if (playerB is not null)
            {
                names[playerB.Id] = playerB.Name;
            }

            return new PlayerComparisonResponse(
                result.Status,
                result.Message,
                ToPlayer(playerA),
                ToPlayer(playerB),
                result.CountedMatchCount,
                result.PlayerAWins,
                result.PlayerBWins,
                Percentage(result.PlayerAWins, result.CountedMatchCount),
                Percentage(result.PlayerBWins, result.CountedMatchCount),
                result.Meetings.Select(meeting => new HeadToHeadMeetingResponse(
                    meeting.MatchId,
                    meeting.SourceKey,
                    meeting.PlayedOn,
                    meeting.TournamentName,
                    meeting.Surface,
                    meeting.Round,
                    meeting.WinnerId,
                    names.TryGetValue(meeting.WinnerId, out var winner) ? winner : null,
                    meeting.Score,
                    null,
                    null)).ToArray(),
                odds);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException)
        {
            return new PlayerComparisonResponse(
                OddsStatuses.DatabaseError,
                "The comparison could not be loaded.",
                null,
                null,
                0,
                0,
                0,
                null,
                null,
                [],
                odds);
        }
        catch (Exception)
        {
            return new PlayerComparisonResponse(
                OddsStatuses.CalculationError,
                "The comparison could not be calculated.",
                null,
                null,
                0,
                0,
                0,
                null,
                null,
                [],
                odds);
        }
    }

    private async Task<ComparisonPlayerResponse?> TryPlayerAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return ToPlayer(await _players.FindAsync(id, cancellationToken));
        }
        catch (Exception exception) when (exception is DbException or TimeoutException)
        {
            return null;
        }
    }

    private static ComparisonPlayerResponse? ToPlayer(PlayerSnapshot? player)
    {
        if (player is null)
        {
            return null;
        }

        return new ComparisonPlayerResponse(player.Id, player.Name, player.SourceId, player.Nationality, player.MatchCount, player.Wins, player.Losses);
    }

    private static decimal? Percentage(int wins, int total)
    {
        if (total == 0)
        {
            return null;
        }

        return decimal.Round(100m * wins / total, 2, MidpointRounding.AwayFromZero);
    }
}
