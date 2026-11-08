using Tennis.Api.Import;

namespace Tennis.Api.UnitTests;

public class SeasonHeaderPlanTests
{
    [Fact]
    public void Classify_keeps_winner_code_and_side_prefixes_and_lists_the_rest()
    {
        var plan = SeasonHeaderPlan.Classify(["winner_code", "home_example", "away_example", "not_a_known_field"]);

        Assert.Equal(0, plan.WinnerCodeIndex);
        Assert.Equal([1], plan.HomeIndexes);
        Assert.Equal([2], plan.AwayIndexes);
        Assert.Equal(["not_a_known_field"], plan.UnmappedColumns);
    }

    [Fact]
    public void Unpublished_fields_name_the_bindings_that_have_no_column()
    {
        Assert.Contains(TennisDataBinding.Unpublished, field => field.Field == "Player.Name");
        Assert.Contains(TennisDataBinding.Unpublished, field => field.Field == "Match.Score");
        Assert.Contains(TennisDataBinding.Unpublished, field => field.Field == "MatchPlayer.Odds");
        Assert.DoesNotContain(TennisDataBinding.Unpublished, field => field.Field == "Match.WinnerCode");
    }

    [Fact]
    public void ReadColumns_accepts_an_array_and_ignores_an_object()
    {
        Assert.Equal(["winner_code"], TennisDataBinding.ReadColumns("[\"winner_code\"]"));
        Assert.Empty(TennisDataBinding.ReadColumns("{}"));
        Assert.Empty(TennisDataBinding.ReadColumns(null));
    }
}
