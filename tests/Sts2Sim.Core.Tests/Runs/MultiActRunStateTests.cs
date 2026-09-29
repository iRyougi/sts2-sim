using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

public sealed class MultiActRunStateTests : IDisposable
{
    public MultiActRunStateTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void RunState_SingleAct_CurrentActIndexStaysZero()
    {
        var runState = new RunState("multi-act-single", new Overgrowth());

        Assert.Equal(0, runState.CurrentActIndex);
        Assert.IsType<Overgrowth>(runState.Act);
    }

    [Fact]
    public void RunState_AdvanceAct_IncrementsIndex()
    {
        var runState = CreateTwoActRun();

        runState.AdvanceToNextAct();

        Assert.Equal(1, runState.CurrentActIndex);
        Assert.IsType<FakeSecondAct>(runState.Act);
    }

    [Fact]
    public void RunState_AdvanceAct_RebuildsMap()
    {
        var runState = CreateTwoActRun();
        ActMap firstMap = runState.Map;

        runState.AdvanceToNextAct();

        Assert.NotSame(firstMap, runState.Map);
        Assert.Equal(FakeSecondAct.RoomCount + 1, runState.Map.GetRowCount());
    }

    [Fact]
    public void RunState_AdvanceAct_SwapsEventPool()
    {
        var runState = new RunState("multi-act-shared-20", [new Overgrowth(), new FakeSecondAct()]);
        runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), runState));

        runState.AdvanceToNextAct();

        // Shared-Ancient allocation now precedes room generation, so verify the switched pool
        // without coupling this progression test to the old first-card shuffle snapshot.
        Type[] events = Enumerable.Range(0, runState.Act.EffectiveEventPool.Count)
            .Select(_ => runState.PullNextEvent()).ToArray();
        Assert.Contains(typeof(Neow), events);
        Assert.All(events, type => Assert.Contains(type, runState.Act.EffectiveEventPool));
    }

    [Fact]
    public void RunState_AdvanceAct_PreservesPlayerAndDeck()
    {
        var runState = CreateTwoActRun();
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        CardModel removedCard = player.Deck.Cards[0];
        player.Deck.RemoveInternal(removedCard);
        var distinctiveCard = (CardModel)ModelDb.Card<FallingStar>().MutableClone();
        distinctiveCard.AssignOwner(player);
        player.Deck.AddInternal(distinctiveCard);
        var distinctiveRelic = (RelicModel)ModelDb.Relic<Vajra>().MutableClone();
        distinctiveRelic.AssignOwner(player);
        player.AddRelicInternal(distinctiveRelic);
        player.Creature.LoseHpInternal(7, default);
        int hpBefore = player.Creature.CurrentHp;

        runState.AdvanceToNextAct();

        Assert.Same(player, Assert.Single(runState.Players));
        Assert.Equal(hpBefore, player.Creature.CurrentHp);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, removedCard));
        Assert.Contains(player.Deck.Cards, card => ReferenceEquals(card, distinctiveCard));
        Assert.Contains(player.Relics, relic => ReferenceEquals(relic, distinctiveRelic));
    }

    [Fact]
    public void RunState_AdvanceAct_ResetsUnknownMapPointOddsToConfiguredBases()
    {
        var runState = CreateTwoActRun();
        var odds = runState.Odds.UnknownMapPoint;
        odds.SetBaseOdds(RoomType.Monster, 0.21f);
        odds.SetBaseOdds(RoomType.Elite, -0.4f);
        odds.SetBaseOdds(RoomType.Treasure, 0.08f);
        odds.SetBaseOdds(RoomType.Shop, 0.13f);
        odds.MonsterOdds = 0.91f;
        odds.EliteOdds = 0.3f;
        odds.TreasureOdds = 0.52f;
        odds.ShopOdds = 0.67f;

        runState.AdvanceToNextAct();

        Assert.Equal(0.21f, odds.MonsterOdds);
        Assert.Equal(-0.4f, odds.EliteOdds);
        Assert.Equal(0.08f, odds.TreasureOdds);
        Assert.Equal(0.13f, odds.ShopOdds);
    }

    [Theory]
    [MemberData(nameof(InvalidActLists))]
    public void RunState_InvalidActList_ThrowsArgumentException(string _, ActDefinition[] acts)
    {
        Assert.Throws<ArgumentException>(() => new RunState("invalid-acts", acts));
    }

    [Fact]
    public void RunState_Acts_RejectsMutationThroughReadOnlySurface()
    {
        var runState = CreateTwoActRun();
        IList<ActDefinition> acts = Assert.IsAssignableFrom<IList<ActDefinition>>(runState.Acts);

        Assert.Throws<NotSupportedException>(() => acts[0] = new FakeSecondAct(index: 0));

        Assert.IsType<Overgrowth>(runState.Act);
    }

    [Fact]
    public void RunState_AdvanceBeyondLastAct_Throws()
    {
        var runState = CreateTwoActRun();
        runState.AdvanceToNextAct();

        Assert.Throws<InvalidOperationException>(runState.AdvanceToNextAct);
    }

    private static RunState CreateTwoActRun() =>
        new("multi-act", [new Overgrowth(), new FakeSecondAct()]);


    public static IEnumerable<object[]> InvalidActLists()
    {
        yield return ["null element", new ActDefinition[] { new Overgrowth(), null! }];
        yield return ["non-zero start", new ActDefinition[] { new FakeSecondAct(index: 1) }];
        yield return ["duplicate", new ActDefinition[] { new Overgrowth(), new FakeSecondAct(index: 0) }];
        yield return ["gap", new ActDefinition[] { new Overgrowth(), new FakeSecondAct(index: 2) }];
        yield return ["out of order", new ActDefinition[] { new FakeSecondAct(index: 1), new FakeSecondAct(index: 0) }];
    }

    private sealed class FakeSecondAct(int index = 1) : ActDefinition
    {
        public const int RoomCount = 14;

        private static readonly IReadOnlyList<EncounterDefinition> Encounters =
        [
            new EncounterDefinition(
                (Func<MonsterModel>)(() =>
                    throw new NotSupportedException("The RunState progression test does not enter encounters."))),
        ];

        public override int Index => index;

        public override IReadOnlyList<Type> EventPool => [typeof(Neow)];
        public override IReadOnlyList<Type> AncientPool => [typeof(Sts2Sim.Core.Models.Events.Neow)];

        public override int BaseNumberOfRooms => RoomCount;

        public override int NumberOfWeakEncounters => 0;

        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => Encounters;

        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => Encounters;

        protected override IReadOnlyList<EncounterDefinition> BossEncounters => Encounters;

        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);
    }
}
