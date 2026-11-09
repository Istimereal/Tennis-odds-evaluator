using System.Data.Common;
using Tennis.Api.Odds;
using Tennis.Api.Services;

namespace Tennis.Api.UnitTests;

public class HeadToHeadOddsTests
{
    private readonly Guid _playerA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly Guid _playerB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private readonly HeadToHeadCalculationService _calculation = new();

    [Fact]
    public void Six_and_four_is_60_and_40_with_fair_decimal_odds()
    {
        var result = _calculation.Calculate(_playerA, _playerB, Sample(6, 4));

        Assert.Equal(OddsStatuses.Ok, result.Status);
        Assert.Equal(10, result.CountedMatchCount);
        Assert.Equal(0.6000m, result.PlayerAProbability);
        Assert.Equal(0.4000m, result.PlayerBProbability);
        Assert.Equal(1.6667m, result.PlayerADecimalOdds);
        Assert.Equal(2.5000m, result.PlayerBDecimalOdds);
    }

    [Fact]
    public void Reversing_players_reverses_probabilities_and_ignores_home_away()
    {
        var stored = Sample(6, 4);
        var flipped = stored.Select(FlipSides).ToArray();
        var forward = _calculation.Calculate(_playerA, _playerB, stored);
        var reversed = _calculation.Calculate(_playerB, _playerA, flipped);

        Assert.Equal(forward.PlayerAProbability, reversed.PlayerBProbability);
        Assert.Equal(forward.PlayerBProbability, reversed.PlayerAProbability);
        Assert.Equal(forward.PlayerADecimalOdds, reversed.PlayerBDecimalOdds);
        Assert.Equal(6, reversed.PlayerBWins);
        Assert.Equal(4, reversed.PlayerAWins);
    }

    [Fact]
    public void No_meetings_returns_no_head_to_head()
    {
        var result = _calculation.Calculate(_playerA, _playerB, []);

        Assert.Equal(OddsStatuses.NoHeadToHead, result.Status);
        Assert.Equal(OddsStatuses.NoMatchingPossible, result.Message);
        Assert.Null(result.PlayerAProbability);
        Assert.Null(result.PlayerADecimalOdds);
        Assert.Empty(result.Meetings);
    }

    [Fact]
    public void Sweep_uses_100_and_0_without_dividing_by_zero()
    {
        var result = _calculation.Calculate(_playerA, _playerB, Sample(3, 0));

        Assert.Equal(1.0000m, result.PlayerAProbability);
        Assert.Equal(0.0000m, result.PlayerBProbability);
        Assert.Equal(1.0000m, result.PlayerADecimalOdds);
        Assert.Null(result.PlayerBDecimalOdds);
    }

    [Fact]
    public async Task Same_player_is_invalid_and_does_not_calculate()
    {
        var spy = new SpyCalculation();
        var generator = Generator(spy, new FakeDirectory(_playerA, _playerB), new FakeReader([]));

        var result = await generator.GenerateAsync(_playerA, _playerA, null, CancellationToken.None);

        Assert.Equal(OddsStatuses.InvalidPlayerSelection, result.Status);
        Assert.Equal(0, spy.Calls);
        Assert.Null(result.Combined);
        Assert.Null(result.MatchContext);
    }

    [Fact]
    public async Task Missing_player_is_not_treated_as_no_meetings()
    {
        var spy = new SpyCalculation();
        var generator = Generator(spy, new FakeDirectory(_playerA, null), new FakeReader([]));

        var result = await generator.GenerateAsync(_playerA, _playerB, null, CancellationToken.None);

        Assert.Equal(OddsStatuses.PlayerNotFound, result.Status);
        Assert.NotEqual(OddsStatuses.NoHeadToHead, result.Status);
        Assert.Equal(0, spy.Calls);
    }

    [Fact]
    public void Duplicate_source_matches_count_once()
    {
        var first = Meeting(1, _playerA, winnerCode: 1);
        var duplicate = first with { MatchId = Guid.NewGuid(), Score = "6-4" };
        var result = _calculation.Calculate(_playerA, _playerB, [first, duplicate]);

        Assert.Equal(1, result.CountedMatchCount);
        Assert.Equal(1, result.PlayerAWins);
        Assert.Single(result.Meetings);
    }

    [Fact]
    public void Incomplete_matches_are_excluded_and_are_not_zero_meetings()
    {
        var cancelled = Meeting(1, _playerA, winnerCode: 1) with { Status = "cancelled" };
        var onlyCancelled = _calculation.Calculate(_playerA, _playerB, [cancelled]);
        Assert.Equal(OddsStatuses.InsufficientData, onlyCancelled.Status);
        Assert.NotEqual(OddsStatuses.NoHeadToHead, onlyCancelled.Status);
        Assert.Null(onlyCancelled.PlayerAProbability);

        var finished = Meeting(2, _playerB, winnerCode: 1) with { Status = "FINISHED", StatusExtra = "RETIRED" };
        var mixed = _calculation.Calculate(_playerA, _playerB, [cancelled, finished]);
        Assert.Equal(1, mixed.CountedMatchCount);
        Assert.Equal(1, mixed.PlayerBWins);
    }

    [Fact]
    public async Task Odds_generator_uses_the_head_to_head_service()
    {
        var spy = new SpyCalculation();
        var matches = Sample(6, 4);
        var generator = Generator(spy, new FakeDirectory(_playerA, _playerB), new FakeReader(matches));

        var result = await generator.GenerateAsync(_playerA, _playerB, null, CancellationToken.None);
        var direct = _calculation.Calculate(_playerA, _playerB, matches);

        Assert.Equal(1, spy.Calls);
        Assert.Equal(OddsStatuses.HeadToHeadMethod, result.Method);
        Assert.Equal(direct.PlayerAProbability, result.PlayerAProbability);
        Assert.Equal(direct.PlayerBDecimalOdds, result.PlayerBDecimalOdds);
        Assert.Equal(direct.CountedMatchCount, result.HeadToHeadMatchCount);
        Assert.Null(result.Combined);
        Assert.Null(result.MatchContext);
        Assert.Contains(result.Methods, method => method.Method == OddsStatuses.HeadToHeadMethod);
    }

    [Fact]
    public async Task Database_failure_is_separate_from_no_meetings()
    {
        var spy = new SpyCalculation();
        var generator = Generator(spy, new FakeDirectory(_playerA, _playerB), new ThrowingReader());

        var result = await generator.GenerateAsync(_playerA, _playerB, null, CancellationToken.None);

        Assert.Equal(OddsStatuses.DatabaseError, result.Status);
        Assert.NotEqual(OddsStatuses.NoHeadToHead, result.Status);
        Assert.Null(result.PlayerAProbability);
    }

    private IReadOnlyList<SourceMatch> Sample(int winsA, int winsB)
    {
        var matches = new List<SourceMatch>();
        for (var index = 0; index < winsA; index++)
        {
            matches.Add(Meeting(index + 1, _playerA, 1));
        }

        for (var index = 0; index < winsB; index++)
        {
            matches.Add(Meeting(winsA + index + 1, _playerB, 1));
        }

        return matches;
    }

    private SourceMatch Meeting(int number, Guid winner, short winnerCode)
    {
        var home = winner;
        var away = winner == _playerA ? _playerB : _playerA;
        return new SourceMatch(
            Guid.Parse($"00000000-0000-0000-0000-{number:000000000000}"),
            "source-" + number,
            winnerCode,
            "FINISHED",
            "FINISHED",
            home,
            away,
            winnerCode == 1,
            winnerCode == 2,
            new DateOnly(2021, 1, number),
            "Test",
            "hard",
            "Final",
            "6-4");
    }

    private static SourceMatch FlipSides(SourceMatch match)
    {
        var flippedCode = match.WinnerCode switch
        {
            1 => (short)2,
            2 => (short)1,
            _ => match.WinnerCode
        };
        return match with
        {
            HomePlayerId = match.AwayPlayerId,
            AwayPlayerId = match.HomePlayerId,
            HomeIsWinner = match.AwayIsWinner,
            AwayIsWinner = match.HomeIsWinner,
            WinnerCode = flippedCode
        };
    }

    private static OddsGeneratorService Generator(HeadToHeadCalculationService calculation, IPlayerDirectory players, IHeadToHeadMatchReader matches)
    {
        return new OddsGeneratorService(players, [new HeadToHeadOddsMethod(matches, calculation)]);
    }

    private sealed class SpyCalculation : HeadToHeadCalculationService
    {
        public int Calls { get; private set; }

        public override HeadToHeadResult Calculate(Guid playerAId, Guid playerBId, IReadOnlyList<SourceMatch> matches)
        {
            Calls++;
            return base.Calculate(playerAId, playerBId, matches);
        }
    }

    private sealed class FakeDirectory : IPlayerDirectory
    {
        private readonly PlayerSnapshot? _playerA;
        private readonly PlayerSnapshot? _playerB;

        public FakeDirectory(Guid? playerA, Guid? playerB)
        {
            _playerA = playerA is null ? null : new PlayerSnapshot(playerA.Value, "Player A", "a", null, 0, 0, 0);
            _playerB = playerB is null ? null : new PlayerSnapshot(playerB.Value, "Player B", "b", null, 0, 0, 0);
        }

        public Task<PlayerSnapshot?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            if (_playerA?.Id == id)
            {
                return Task.FromResult<PlayerSnapshot?>(_playerA);
            }

            if (_playerB?.Id == id)
            {
                return Task.FromResult<PlayerSnapshot?>(_playerB);
            }

            return Task.FromResult<PlayerSnapshot?>(null);
        }
    }

    private sealed class FakeReader : IHeadToHeadMatchReader
    {
        private readonly IReadOnlyList<SourceMatch> _matches;

        public FakeReader(IReadOnlyList<SourceMatch> matches)
        {
            _matches = matches;
        }

        public Task<IReadOnlyList<SourceMatch>> LoadPairAsync(Guid playerAId, Guid playerBId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_matches);
        }
    }

    private sealed class ThrowingReader : IHeadToHeadMatchReader
    {
        public Task<IReadOnlyList<SourceMatch>> LoadPairAsync(Guid playerAId, Guid playerBId, CancellationToken cancellationToken)
        {
            throw new FakeDbException();
        }
    }

    private sealed class FakeDbException : DbException
    {
        public FakeDbException()
            : base("database unavailable")
        {
        }
    }
}
