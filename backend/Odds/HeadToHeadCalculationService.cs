namespace Tennis.Api.Odds;

public class HeadToHeadCalculationService
{
    private static readonly HashSet<string> CompletedMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "finished",
        "retired",
        "walkover",
        "awarded"
    };

    private static readonly HashSet<string> ExcludedMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        "cancelled",
        "canceled",
        "postponed",
        "unfinished",
        "invalid",
        "live",
        "scheduled",
        "suspended",
        "interrupted",
        "abandoned",
        "incomplete",
        "not started"
    };

    public virtual HeadToHeadResult Calculate(Guid playerAId, Guid playerBId, IReadOnlyList<SourceMatch> matches)
    {
        var shared = matches
            .Where(match => Involves(match, playerAId, playerBId))
            .GroupBy(SourceIdentity, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(IsCountable)
                .ThenByDescending(match => !string.IsNullOrWhiteSpace(match.Score))
                .ThenBy(match => match.MatchId)
                .First())
            .ToArray();

        if (shared.Length == 0)
        {
            return Empty(OddsStatuses.NoHeadToHead, OddsStatuses.NoMatchingPossible, 0);
        }

        var counted = new List<CountedMeeting>();
        foreach (var match in shared)
        {
            if (!TryReadWinner(match, out var winnerId))
            {
                continue;
            }

            if (winnerId != playerAId && winnerId != playerBId)
            {
                continue;
            }

            counted.Add(new CountedMeeting(
                match.MatchId,
                match.SourceKey,
                winnerId,
                match.PlayedOn,
                match.TournamentName,
                match.Surface,
                match.Round,
                match.Score));
        }

        if (counted.Count == 0)
        {
            return Empty(
                OddsStatuses.InsufficientData,
                "Head-to-head records exist, but none have a completed result with an identifiable winner.",
                shared.Length);
        }

        var ordered = counted
            .OrderByDescending(meeting => meeting.PlayedOn)
            .ThenBy(meeting => meeting.SourceKey, StringComparer.Ordinal)
            .ThenBy(meeting => meeting.MatchId)
            .ToArray();
        var winsA = ordered.Count(meeting => meeting.WinnerId == playerAId);
        var winsB = ordered.Count(meeting => meeting.WinnerId == playerBId);
        var total = ordered.Length;
        return new HeadToHeadResult(
            OddsStatuses.Ok,
            "Head-to-head odds from completed meetings.",
            shared.Length,
            total,
            winsA,
            winsB,
            Probability(winsA, total),
            Probability(winsB, total),
            DecimalOdds(winsA, total),
            DecimalOdds(winsB, total),
            ordered);
    }

    public static decimal Probability(int wins, int total) =>
        decimal.Round(wins / (decimal)total, 4, MidpointRounding.AwayFromZero);

    public static decimal? DecimalOdds(int wins, int total)
    {
        if (wins <= 0)
        {
            return null;
        }

        return decimal.Round(total / (decimal)wins, 4, MidpointRounding.AwayFromZero);
    }

    private static HeadToHeadResult Empty(string status, string message, int shared) =>
        new(status, message, shared, 0, 0, 0, null, null, null, null, []);

    private static string SourceIdentity(SourceMatch match)
    {
        var key = match.SourceKey.Trim();
        return key.Length == 0 ? "match:" + match.MatchId.ToString("N") : "source:" + key;
    }

    private static bool Involves(SourceMatch match, Guid playerAId, Guid playerBId)
    {
        if (match.HomePlayerId is null || match.AwayPlayerId is null)
        {
            return false;
        }

        return (match.HomePlayerId == playerAId && match.AwayPlayerId == playerBId)
            || (match.HomePlayerId == playerBId && match.AwayPlayerId == playerAId);
    }

    private static bool IsCountable(SourceMatch match) => TryReadWinner(match, out _);

    private static bool TryReadWinner(SourceMatch match, out Guid winnerId)
    {
        winnerId = Guid.Empty;
        if (!IsCompleted(match.Status, match.StatusExtra))
        {
            return false;
        }

        Guid? fromCode = match.WinnerCode switch
        {
            1 => match.HomePlayerId,
            2 => match.AwayPlayerId,
            _ => null
        };
        Guid? fromFlag = (match.HomeIsWinner, match.AwayIsWinner) switch
        {
            (true, true) => null,
            (true, false) => match.HomePlayerId,
            (false, true) => match.AwayPlayerId,
            (true, null) => match.HomePlayerId,
            (null, true) => match.AwayPlayerId,
            _ => null
        };
        if (fromCode is null && fromFlag is null)
        {
            return false;
        }

        if (fromCode is not null && fromFlag is not null && fromCode != fromFlag)
        {
            return false;
        }

        var winner = fromCode ?? fromFlag;
        if (winner is null || (winner != match.HomePlayerId && winner != match.AwayPlayerId))
        {
            return false;
        }

        winnerId = winner.Value;
        return true;
    }

    private static bool IsCompleted(string? status, string? statusExtra)
    {
        if (IsExcluded(status) || IsExcluded(statusExtra))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return CompletedMarkers.Contains(status.Trim());
    }

    private static bool IsExcluded(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && ExcludedMarkers.Contains(value.Trim());
    }
}
