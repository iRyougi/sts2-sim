using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class NeowsBonesTests : IDisposable
{
    private abstract class CurseSensitiveRelic : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Event;

        public override Task AfterObtained()
        {
            Assert.DoesNotContain(Owner.Deck.Cards, card => card.Rarity == CardRarity.Curse);
            Owner.RunState.Rng.Niche.NextInt();
            return Task.CompletedTask;
        }
    }

    private sealed class AllowedRelicA : CurseSensitiveRelic
    {
    }

    private sealed class AllowedRelicB : CurseSensitiveRelic
    {
    }

    private sealed class AllowedRelicC : CurseSensitiveRelic
    {
    }

    private sealed class DisallowedRelic : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Event;
        public override bool IsAllowedAtNeow(IRunState runState) => false;
    }

    private sealed class EventOnlyDecisionSource : IRunDecisionSource
    {
        public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
            throw new InvalidOperationException("Map decisions are not expected while driving this event.");

        public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
            throw new InvalidOperationException("Combat decisions are not expected while driving this event.");
    }

    private sealed class GivesBonesAncientEvent : AncientEventModel
    {
        private readonly bool _hasCandidates;

        public GivesBonesAncientEvent(bool hasCandidates)
        {
            _hasCandidates = hasCandidates;
        }

        public override IReadOnlyList<RelicModel> AllPossibleOptions => _hasCandidates
            ? new RelicModel[]
            {
                ModelDb.Relic<NeowsBones>(),
                new DisallowedRelic(),
                new AllowedRelicA(),
                new AllowedRelicB(),
                new AllowedRelicC(),
            }
            : new RelicModel[] { ModelDb.Relic<NeowsBones>(), new DisallowedRelic() };

        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
            new[]
            {
                new EventOption("TAKE", async () =>
                {
                    await RelicCmd.Obtain(ModelDb.Relic<NeowsBones>(), Owner);
                    Finish();
                }),
            };
    }

    private sealed class OrderReward(Player player, List<string> order, string label) : TakeableReward(player)
    {
        public override void Populate(IRunState runState) { }
        protected override Task OnTake()
        {
            order.Add(label);
            return Task.CompletedTask;
        }
    }

    private sealed class NestedOfferRelic(List<string> order) : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Event;
        public override Task AfterObtained()
        {
            order.Add("parent relic");
            OfferRewards(RewardsSet.CreateCustom(Owner,
                extraRewards: [new OrderReward(Owner, order, "first child")]));
            OfferRewards(RewardsSet.CreateCustom(Owner,
                extraRewards: [new OrderReward(Owner, order, "second child")]));
            return Task.CompletedTask;
        }
    }

    private sealed class NestedOfferEvent(List<string> order) : EventModel
    {
        protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("TAKE", () =>
        {
            OfferRewards(RewardsSet.CreateCustom(Owner, extraRewards:
            [
                new RelicReward((RelicModel)new NestedOfferRelic(order).MutableClone(), Owner),
                new OrderReward(Owner, order, "parent sibling"),
            ]));
            OfferRewards(RewardsSet.CreateCustom(Owner,
                extraRewards: [new OrderReward(Owner, order, "parent continuation")]));
            Finish();
            return Task.CompletedTask;
        })];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NestedOffers_FinishChildrenInOrderBeforeParentSiblingAndContinuation(bool useDriver)
    {
        // Native awaits a taken relic's nested RewardsSet.Offer before returning to
        // the parent set; NeowsBones applies its curse only after that set finishes.
        Player player = CreatePlayer("issue-53-nested-offers");
        var run = (RunState)player.RunState;
        var order = new List<string>();
        var room = new EventRoom(() => (EventModel)new NestedOfferEvent(order).MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        if (useDriver)
            await new RunDriver(run, new EventOnlyDecisionSource()).DriveEventAsync(room);
        else
            await RunEngine.DriveEventToCompletion(room);

        Assert.Equal(new[] { "parent relic", "first child", "second child", "parent sibling", "parent continuation" }, order);
        Assert.False(room.Event.HasPendingRewardOffers);
    }

    public NeowsBonesTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();
    [Fact]
    public async Task ArchivedJGENKZEW4VNW_NeowsBonesAddsInjuryAfterItsRelicRewards()
    {
        // F1 archive: NeowsBones grants LeafyPoultice + NutritiousOyster, then Injury;
        // Leafy transforms a Strike/Defend into BladeDance/PreciseCut. No factory oracle.
        var run = new RunState("JGENKZEW4VNW", ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord);
        var room = Assert.IsType<EventRoom>(RoomFactory.CreateAncientEventRoom(run));
        run.PushRoom(room);
        try
        {
            await room.Enter(run);
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(option => option.Key == "NeowsBones"));
            await RunEngine.DriveEventToCompletion(room);

            Assert.Contains(player.Relics, relic => relic is LeafyPoultice);
            Assert.Contains(player.Relics, relic => relic is NutritiousOyster);
            Assert.Single(player.Deck.Cards, card => card is BladeDance);
            Assert.Single(player.Deck.Cards, card => card is PreciseCut);
            CardModel curse = Assert.Single(player.Deck.Cards,
                card => card.Rarity == CardRarity.Curse && card is not AscendersBane);
            Assert.IsType<Injury>(curse);
            Assert.Same(player, curse.Owner);
            Assert.Same(player.Deck, curse.Pile);
            // These exclusions are explicit in each authoritative card class.
            Assert.False(ModelDb.Card<Greed>().CanBeGeneratedByModifiers);
            Assert.False(ModelDb.Card<PoorSleep>().CanBeGeneratedByModifiers);
            Assert.False(ModelDb.Card<SporeMind>().CanBeGeneratedByModifiers);
            Assert.False(room.Event.HasPendingRewardOffers);
        }
        finally
        {
            await room.Exit(run);
            run.PopCurrentRoom();
        }
    }

    [Fact]
    public async Task PredeterminedRelicReward_PopulatePreservesMutableRelicWithoutAdvancingRng()
    {
        Player player = CreatePlayer("bones-predetermined-reward");
        var predetermined = (RelicModel)new AllowedRelicA().MutableClone();
        var reward = new RelicReward(predetermined, player);
        int rewardsBefore = player.PlayerRng.Rewards.Counter;

        reward.Populate(player.RunState);
        reward.Populate(player.RunState);
        await reward.Take();

        Assert.Same(predetermined, reward.Relic);
        Assert.Equal(rewardsBefore, player.PlayerRng.Rewards.Counter);
        AllowedRelicA owned = Assert.Single(player.Relics.OfType<AllowedRelicA>());
        Assert.NotSame(predetermined, owned);
        Assert.Same(player, owned.Owner);
        Assert.True(reward.IsResolved);
    }

    [Fact]
    public async Task ProductionEventDrain_FiltersShufflesAndResolvesBothPredeterminedRelicsThenCurse()
    {
        Player player = CreatePlayer("neows-bones-bane-1");
        var runState = (RunState)player.RunState;
        EventRoom room = await EnterAncient(runState, hasCandidates: true);
        var ancient = Assert.IsType<GivesBonesAncientEvent>(room.Event);
        List<Type> expectedTypes = ancient.AllPossibleOptions
            .Where(relic => relic is not NeowsBones && relic.IsAllowedAtNeow(runState))
            .Select(relic => relic.GetType())
            .ToList();
        var rewardsOracle = player.PlayerRng.Rewards.CloneExact();
        rewardsOracle.Shuffle(expectedTypes);
        Type[] expectedRelics = expectedTypes.Take(2).ToArray();
        List<CardModel> curseCandidates = CardPoolFilters.ForSinglePlayer(ModelDb.All<CardModel>())
            .Where(card => card.Rarity == CardRarity.Curse && card.CanBeGeneratedByModifiers)
            .ToList();
        var nicheOracle = runState.Rng.Niche.CloneExact();
        nicheOracle.NextInt();
        Assert.DoesNotContain(curseCandidates, card => !card.CanBeGeneratedByModifiers);
        nicheOracle.NextInt();
        CardModel expectedCurse = Assert.IsAssignableFrom<CardModel>(nicheOracle.NextItem(curseCandidates));
        int deckBefore = player.Deck.Cards.Count;
        int relicsBefore = player.Relics.Count;

        await RunEngine.DriveEventToCompletion(room);

        RelicModel[] obtained = player.Relics.Skip(relicsBefore).ToArray();
        Assert.Equal(3, obtained.Length);
        NeowsBones bones = Assert.IsType<NeowsBones>(obtained[0]);
        Assert.Equal(expectedRelics, obtained.Skip(1).Select(relic => relic.GetType()));
        Assert.All(obtained, relic => Assert.Same(player, relic.Owner));
        CardModel curse = Assert.Single(player.Deck.Cards.Skip(deckBefore));
        Assert.Equal(expectedCurse.Id, curse.Id);
        Assert.Equal(CardRarity.Curse, curse.Rarity);
        Assert.False(curse.IsCanonical);
        Assert.Same(player, curse.Owner);
        Assert.IsNotType<AscendersBane>(curse);
        Assert.Same(player.Deck, curse.Pile);
        Assert.Equal(rewardsOracle.Counter, player.PlayerRng.Rewards.Counter);
        Assert.Equal(nicheOracle.Counter, runState.Rng.Niche.Counter);
        Assert.Equal(RelicRarity.Ancient, bones.Rarity);
        Assert.False(bones.HasUponPickupEffect);
        Assert.False(ancient.HasPendingRewardOffers);
    }

    [Fact]
    public async Task RunDriverProductionDrain_ResolvesBothPredeterminedRelicsBeforeCurse()
    {
        Player player = CreatePlayer("neows-bones-driver");
        var runState = (RunState)player.RunState;
        EventRoom room = await EnterAncient(runState, hasCandidates: true);
        int deckBefore = player.Deck.Cards.Count;
        int relicsBefore = player.Relics.Count;
        var driver = new RunDriver(runState, new EventOnlyDecisionSource());

        await driver.DriveEventAsync(room);

        Assert.Equal(3, player.Relics.Count - relicsBefore);
        Assert.Single(player.Deck.Cards.Skip(deckBefore), card => card.Rarity == CardRarity.Curse);
        Assert.False(room.Event.HasPendingRewardOffers);
    }

    [Fact]
    public async Task AfterObtained_OutsideCurrentAncientEvent_ThrowsBeforeRngOrCurseMutation()
    {
        Player player = CreatePlayer("neows-bones-guard");
        var runState = (RunState)player.RunState;
        var room = new EventRoom(() => (EventModel)ModelDb.Event<JungleMazeAdventure>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        var bones = (NeowsBones)ModelDb.Relic<NeowsBones>().MutableClone();
        bones.AssignOwner(player);
        int rewardsBefore = player.PlayerRng.Rewards.Counter;
        int nicheBefore = runState.Rng.Niche.Counter;
        int deckBefore = player.Deck.Cards.Count;

        await Assert.ThrowsAsync<InvalidOperationException>(() => bones.AfterObtained());

        Assert.Equal(rewardsBefore, player.PlayerRng.Rewards.Counter);
        Assert.Equal(nicheBefore, runState.Rng.Niche.Counter);
        Assert.Equal(deckBefore, player.Deck.Cards.Count);
        Assert.False(room.Event.HasPendingRewardOffers);
    }

    [Fact]
    public async Task ProductionEventDrain_WithNoEligibleRelicCandidates_StillAddsOneCurse()
    {
        Player player = CreatePlayer("neows-bones-no-candidates");
        var runState = (RunState)player.RunState;
        EventRoom room = await EnterAncient(runState, hasCandidates: false);
        int deckBefore = player.Deck.Cards.Count;
        int relicsBefore = player.Relics.Count;

        await RunEngine.DriveEventToCompletion(room);

        Assert.Single(player.Relics.Skip(relicsBefore), relic => relic is NeowsBones);
        Assert.Single(player.Deck.Cards.Skip(deckBefore), card => card.Rarity == CardRarity.Curse);
        Assert.False(room.Event.HasPendingRewardOffers);
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }

    private static async Task<EventRoom> EnterAncient(RunState runState, bool hasCandidates)
    {
        var room = new EventRoom(() =>
            (EventModel)new GivesBonesAncientEvent(hasCandidates).MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        return room;
    }
}
