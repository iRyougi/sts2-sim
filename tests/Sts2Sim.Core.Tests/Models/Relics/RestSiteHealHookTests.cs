using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class RestSiteHealHookTests : IDisposable
{
    public RestSiteHealHookTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(DreamCatcher), typeof(TinyMailbox), typeof(RestHealProbeRelic),
        }).Distinct());
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task RestAndDenseVegetation_ApplyTheSameHealHooks(bool isMimicked, bool useRunDriver)
    {
        var runState = new RunState($"rest-heal-hooks-{isMimicked}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        Player foreign = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.Creature.SetMaxHpInternal(80);
        player.Creature.HealInternal(80);
        player.Creature.LoseHpInternal(79, default(ValueProp));
        await RelicCmd.Obtain(ModelDb.Relic<StoneHumidifier>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<RegalPillow>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<DreamCatcher>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<TinyMailbox>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<RestHealProbeRelic>(), player);
        var probe = Assert.Single(player.Relics.OfType<RestHealProbeRelic>());
        probe.ExecutionFinished += _ =>
        {
            if (probe.AfterIsMimicked is not null && probe.RewardsIsMimicked is null)
            {
                probe.AfterExecutionFinishedCount++;
            }
        };
        int deckBefore = player.Deck.Cards.Count;

        if (isMimicked)
        {
            var room = new EventRoom(() =>
                (EventModel)ModelDb.Event<DenseVegetation>().MutableClone());
            runState.PushRoom(room);
            await room.Enter(runState);
            await room.Event.ChooseOption(room.Event.CurrentOptions.Single(option => option.Key == "REST"));
            Assert.Equal("FIGHT", Assert.Single(room.Event.CurrentOptions).Key);
            Assert.True(room.Event.HasPendingRewardOffers);
            // The static event runner drains the offer without driving Dense Vegetation's forced combat.
            await RunEngine.DriveEventToCompletion(room);
            Assert.False(room.Event.HasPendingRewardOffers);
        }
        else
        {
            runState.Map.StartingMapPoint.PointType = MapPointType.Monster;
            runState.Map.StartingMapPoint.Children.OrderBy(point => point.coord.col).First().PointType =
                MapPointType.RestSite;
            foreach (CardModel card in player.Deck.Cards.Where(card => card.IsUpgradable))
            {
                card.Upgrade();
            }
            if (useRunDriver)
            {
                await new RunDriver(runState, new RestRewardDecisionSource()).RunAsync(maxFloors: 1);
            }
            else
            {
                await new RunEngine(runState, options => options.OrderBy(point => point.coord.col).First())
                    .RunAsync(maxFloors: 1);
            }
        }

        Assert.Equal(85, player.Creature.MaxHp);
        // 1 HP + floor(80 * 0.3) + Regal Pillow's 15 + Stone Humidifier's 5.
        Assert.Equal(45, player.Creature.CurrentHp);
        Assert.Equal(isMimicked, probe.AfterIsMimicked);
        Assert.Equal(isMimicked, probe.RewardsIsMimicked);
        Assert.Equal(45, probe.HpAfterHeal);
        Assert.Equal(45, probe.HpBeforeRewards);
        Assert.Equal(1, probe.AfterExecutionFinishedCount);
        Assert.Equal(RoomType.Unassigned, probe.GeneralRewardsRoomType);
        Assert.Equal(1, probe.GeneralRewardModificationCount);
        IReadOnlyList<Reward> offered = Assert.IsAssignableFrom<IReadOnlyList<Reward>>(probe.OfferedRewards);
        CardReward cards = Assert.IsType<CardReward>(offered[0]);
        Assert.Equal(3, cards.Options.Count);
        Assert.Equal(2, offered.OfType<PotionReward>().Count());
        Assert.Equal(3, offered.Count);
        Assert.All(offered, reward => Assert.True(reward.IsResolved));
        Assert.Equal(deckBefore + 1, player.Deck.Cards.Count);
        Assert.Equal(2, player.PotionSlots.OfType<PotionModel>().Count());
        Assert.Same(useRunDriver ? cards.Options[^1] : cards.Options[0], cards.SelectedOption);
        Assert.Contains(SharedRelicPool.Instance.AllRelics, relic => relic is TinyMailbox);
        Assert.DoesNotContain(SharedRelicPool.Instance.AllRelics, relic => relic is DreamCatcher);

        var foreignRewards = new List<Reward>();
        Assert.Empty(Hook.ModifyRestSiteHealRewards(runState, foreign, foreignRewards, isMimicked));
        Assert.Empty(foreignRewards);
    }
}

file sealed class RestHealProbeRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;
    public bool? AfterIsMimicked { get; private set; }
    public bool? RewardsIsMimicked { get; private set; }
    public int HpAfterHeal { get; private set; }
    public int HpBeforeRewards { get; private set; }
    public int AfterExecutionFinishedCount { get; set; }
    public RoomType? GeneralRewardsRoomType { get; private set; }
    public int GeneralRewardModificationCount { get; private set; }
    public IReadOnlyList<Reward>? OfferedRewards { get; private set; }

    public override async Task AfterRestSiteHeal(Player player, bool isMimicked)
    {
        if (player != Owner) return;
        await Task.Yield();
        AfterIsMimicked = isMimicked;
        HpAfterHeal = player.Creature.CurrentHp;
    }

    public override bool TryModifyRestSiteHealRewards(Player player, List<Reward> rewards, bool isMimicked)
    {
        if (player != Owner) return false;
        RewardsIsMimicked = isMimicked;
        HpBeforeRewards = player.Creature.CurrentHp;
        OfferedRewards = rewards.ToArray();
        return false;
    }

    public override void ModifyRewards(Player player, List<Reward> rewards, RoomType roomType)
    {
        if (player != Owner) return;
        GeneralRewardsRoomType = roomType;
        GeneralRewardModificationCount++;
    }
}

file sealed class RestRewardDecisionSource : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(point => point.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state) =>
        Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());

    public Task<RewardDecision> ChooseRewardActionAsync(RewardsSet rewards)
    {
        CardReward? card = rewards.ExtraRewards.OfType<CardReward>().FirstOrDefault(reward => !reward.IsResolved);
        return Task.FromResult(card is null
            ? RewardDecisionClassifier.ChooseDefault(rewards)
            : (RewardDecision)new RewardDecision.ResolveExtra(card, card.Options[^1]));
    }
}
