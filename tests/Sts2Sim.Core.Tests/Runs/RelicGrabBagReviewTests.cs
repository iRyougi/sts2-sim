using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

file sealed class ReviewCommonRelicA : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
file sealed class ReviewCommonRelicB : RelicModel { public override RelicRarity Rarity => RelicRarity.Common; }
file sealed class ReviewStarterRelic : RelicModel { public override RelicRarity Rarity => RelicRarity.Starter; }

[Collection("ModelDb")]
public class RelicGrabBagReviewTests : IDisposable
{
    public RelicGrabBagReviewTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(ReviewCommonRelicA), typeof(ReviewCommonRelicB), typeof(ReviewStarterRelic), typeof(AmethystAubergine), typeof(Anchor),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void PullingStarter_ReturnsNull_AndMixedEndDrawsDoNotReuseRelics()
    {
        var bag = new RelicGrabBag(new Rng(7u), new RelicModel[]
        {
            ModelDb.Relic<ReviewCommonRelicA>(),
            ModelDb.Relic<ReviewCommonRelicB>(),
            ModelDb.Relic<ReviewStarterRelic>(),
        });

        Assert.Null(bag.PullFromFront(RelicRarity.Starter));

        RelicModel? fromFront = bag.PullFromFront(RelicRarity.Common);
        RelicModel? fromBack = bag.PullFromBack(RelicRarity.Common);

        Assert.NotNull(fromFront);
        Assert.NotNull(fromBack);
        Assert.NotEqual(fromFront!.GetType(), fromBack!.GetType());
        Assert.Null(bag.PullFromFront(RelicRarity.Common));
        Assert.Null(bag.PullFromBack(RelicRarity.Common));
    }

    [Fact]
    public void CreateForNewRun_PopulatesSharedAndPlayerRelicBagsUsingUpFrontStream()
    {
        var runState = new RunState("relic-grab-bag-player", new Overgrowth());
        int treasureCounterBefore = runState.Rng.TreasureRoomRelics.Counter;
        int upFrontCounterBefore = runState.Rng.UpFront.Counter;
        int shuffleCounterBefore = runState.Rng.Shuffle.Counter;

        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);

        Assert.Equal(treasureCounterBefore, runState.Rng.TreasureRoomRelics.Counter);
        Assert.Equal(upFrontCounterBefore + 2, runState.Rng.UpFront.Counter);
        Assert.NotNull(runState.SharedRelicGrabBag);
        Assert.Equal(shuffleCounterBefore, runState.Rng.Shuffle.Counter);

        RelicModel? first = player.RelicGrabBag.PullFromFront(RelicRarity.Common);
        RelicModel? second = player.RelicGrabBag.PullFromFront(RelicRarity.Common);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first!.GetType(), second!.GetType());
        Assert.Null(player.RelicGrabBag.PullFromFront(RelicRarity.Common));
    }
}
