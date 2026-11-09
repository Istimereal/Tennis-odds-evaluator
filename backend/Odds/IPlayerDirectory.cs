namespace Tennis.Api.Odds;

public interface IPlayerDirectory
{
    Task<PlayerSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public interface IHeadToHeadMatchReader
{
    Task<IReadOnlyList<SourceMatch>> LoadPairAsync(Guid playerAId, Guid playerBId, CancellationToken cancellationToken);
}

public interface IOddsMethod
{
    string Method { get; }

    Task<OddsMethodEstimate> EvaluateAsync(OddsEvaluationRequest request, CancellationToken cancellationToken);
}

public sealed record OddsMethodEstimate(
    string Method,
    string Status,
    string Message,
    int HeadToHeadMatchCount,
    decimal? PlayerAProbability,
    decimal? PlayerBProbability,
    decimal? PlayerADecimalOdds,
    decimal? PlayerBDecimalOdds);
