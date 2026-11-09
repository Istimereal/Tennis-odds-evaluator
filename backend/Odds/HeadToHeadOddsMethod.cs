using System.Data.Common;

namespace Tennis.Api.Odds;

public sealed class HeadToHeadOddsMethod : IOddsMethod
{
    private readonly IHeadToHeadMatchReader _matches;
    private readonly HeadToHeadCalculationService _calculation;

    public HeadToHeadOddsMethod(IHeadToHeadMatchReader matches, HeadToHeadCalculationService calculation)
    {
        _matches = matches;
        _calculation = calculation;
    }

    public string Method => OddsStatuses.HeadToHeadMethod;

    public async Task<OddsMethodEstimate> EvaluateAsync(OddsEvaluationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var matches = await _matches.LoadPairAsync(request.PlayerAId, request.PlayerBId, cancellationToken);
            var result = _calculation.Calculate(request.PlayerAId, request.PlayerBId, matches);
            return new OddsMethodEstimate(
                Method,
                result.Status,
                result.Message,
                result.CountedMatchCount,
                result.PlayerAProbability,
                result.PlayerBProbability,
                result.PlayerADecimalOdds,
                result.PlayerBDecimalOdds);
        }
        catch (Exception exception) when (exception is DbException or TimeoutException)
        {
            return Failed(OddsStatuses.DatabaseError, "The head-to-head matches could not be loaded.");
        }
        catch (Exception)
        {
            return Failed(OddsStatuses.CalculationError, "The head-to-head odds could not be calculated.");
        }
    }

    private OddsMethodEstimate Failed(string status, string message) =>
        new(Method, status, message, 0, null, null, null, null);
}
