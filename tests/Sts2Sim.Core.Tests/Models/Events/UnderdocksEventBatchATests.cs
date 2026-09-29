using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Encounters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Events;

file sealed class PunchOffCombatStartProbe : RelicModel
{
    private PunchOff? _event;

    public override RelicRarity Rarity => RelicRarity.Common;

    public IReadOnlyList<Creature>? EnemiesAtStart { get; private set; }

    public int NicheAtStart { get; private set; }

    public int? RewardsAtFirstTurnEnd { get; private set; }

    public bool? ExtrasPopulatedAtFirstTurnEnd { get; private set; }

    public void WatchExtraRewards(PunchOff punchOff) => _event = punchOff;

    public override Task BeforeCombatStart()
    {
        EnemiesAtStart = Owner.Creature.CombatState!.Enemies.ToArray();
        NicheAtStart = Owner.RunState.Rng.Niche.Counter;
        return Task.CompletedTask;
    }

    // 驱动器进战斗之后才登记额外奖励，所以在战斗中途（玩家第一个回合结束）检查它们是否已掷骰。
    public override Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || RewardsAtFirstTurnEnd is not null || _event is null)
            return Task.CompletedTask;

        RewardsAtFirstTurnEnd = Owner.PlayerRng.Rewards.Counter;
        ExtrasPopulatedAtFirstTurnEnd = _event.ForcedCombatExtraRewards.Any(reward => reward switch
        {
            RelicReward relic => relic.Relic is not null,
            PotionReward potion => potion.Potion is not null,
            _ => false,
        });
        return Task.CompletedTask;
    }
}

file sealed class PunchOffAttackDecisionSource : IRunDecisionSource
{
    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options[0]);

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        CardModel? attack = state.Players[0].PlayerCombatState!.Hand.Cards.FirstOrDefault(
            card => card.Type == CardType.Attack &&
                    card.TargetType == TargetType.AnyEnemy &&
                    card.CanPlay(out _));
        return Task.FromResult<CombatDecision>(attack is null
            ? new CombatDecision.EndTurn()
            : new CombatDecision.PlayCard(attack, state.HittableEnemies[0]));
    }
}

[Collection("ModelDb")]
public sealed class UnderdocksEventBatchATests : IDisposable
{
    public UnderdocksEventBatchATests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("AbyssalBaths")]
    [InlineData("DrowningBeacon")]
    [InlineData("EndlessConveyor")]
    [InlineData("PunchOff")]
    [InlineData("SpiralingWhirlpool")]
    public async Task UnderdocksEventBatchA_ExecutesOneMainBranchPerEvent(string eventName)
    {
        Type eventType = eventName switch
        {
            nameof(AbyssalBaths) => typeof(AbyssalBaths),
            nameof(DrowningBeacon) => typeof(DrowningBeacon),
            nameof(EndlessConveyor) => typeof(EndlessConveyor),
            nameof(PunchOff) => typeof(PunchOff),
            nameof(SpiralingWhirlpool) => typeof(SpiralingWhirlpool),
            _ => throw new ArgumentOutOfRangeException(nameof(eventName), eventName, null),
        };

        EventModel eventModel = (EventModel)ModelDb.Get(eventType).MutableClone();
        var runState = new RunState($"underdocks-event-{eventName}", new Overgrowth());
        var player = Sts2Sim.Core.Entities.Players.Player.CreateForNewRun(
            ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.Gold = 200;

        eventModel.AssignOwner(player);
        eventModel.BeginEvent(runState);

        Assert.Equal(eventType, eventModel.GetType());
        Assert.NotEmpty(eventModel.CurrentOptions);

        switch (eventName)
        {
            case nameof(AbyssalBaths):
            {
                int maxHpBefore = player.Creature.MaxHp;
                int hpBefore = player.Creature.CurrentHp;
                await eventModel.ChooseOption(eventModel.CurrentOptions.Single(option => option.Key == "IMMERSE"));
                Assert.Equal(maxHpBefore + 2, player.Creature.MaxHp);
                Assert.Equal(hpBefore + 2 - 3, player.Creature.CurrentHp);
                await eventModel.ChooseOption(eventModel.CurrentOptions.Single(option => option.Key == "EXIT_BATHS"));
                Assert.True(eventModel.IsFinished);
                break;
            }
            case nameof(DrowningBeacon):
            {
                await eventModel.ChooseOption(eventModel.CurrentOptions.Single(option => option.Key == "BOTTLE"));
                Assert.True(eventModel.TryDequeuePendingRewardOffer(out RewardsSet? rewards));
                Assert.Contains(rewards!.ExtraRewards, reward => reward is PotionReward);
                Assert.True(eventModel.IsFinished);
                break;
            }
            case nameof(EndlessConveyor):
            {
                CardModel firstTransformable = player.Deck.Cards.First(card => card.IsTransformable);
                CardModel targetTransformable = player.Deck.Cards.Last(card => card.IsTransformable);
                Assert.NotSame(firstTransformable, targetTransformable);
                runState.ConfigureCardSelectionSource(new LastCardsSelectionSource());
                typeof(EndlessConveyor).GetField("_currentDishId", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(eventModel, "JELLY_LIVER");
                await eventModel.ChooseOption(eventModel.CurrentOptions[0]);
                Assert.Contains(firstTransformable, player.Deck.Cards);
                Assert.DoesNotContain(targetTransformable, player.Deck.Cards);
                await eventModel.ChooseOption(eventModel.CurrentOptions.Single(option => option.Key == "LEAVE"));
                Assert.True(eventModel.IsFinished);
                break;
            }
            case nameof(PunchOff):
            {
                await eventModel.ChooseOption(eventModel.CurrentOptions.Single(option => option.Key == "I_CAN_TAKE_THEM"));
                await eventModel.ChooseOption(eventModel.CurrentOptions.Single(option => option.Key == "FIGHT"));
                Assert.True(eventModel.IsAwaitingForcedCombat);
                Assert.True(eventModel.HasPendingForcedCombat);
                Assert.False(eventModel.IsFinished);
                Assert.Equal(2, eventModel.ForcedCombatExtraRewards.Count);
                var actualFactory = eventModel.DequeuePendingForcedCombatBatch();
                IReadOnlyList<MonsterModel> actualMonsters = actualFactory();
                ulong encounterSeed = unchecked((ulong)((long)runState.Rng.Seed + runState.TotalFloor)) +
                    StringHelper.GetDeterministicHashCode(StringHelper.Slugify(PunchOffEventEncounter.Definition.Name));
                IReadOnlyList<MonsterModel> expectedMonsters = PunchOffEventEncounter.Definition
                    .CreateMonsters(new Rng(encounterSeed))
                    .Select(entry => entry.Monster)
                    .ToArray();
                Assert.Equal(
                    expectedMonsters.Select(monster => ((PunchConstruct)monster).StartingHpReduction),
                    actualMonsters.Select(monster => ((PunchConstruct)monster).StartingHpReduction));
                eventModel.ResumeAfterForcedCombat(new ForcedCombatOutcome(Victory: true, TimedOut: false));
                Assert.True(eventModel.IsFinished);

                var nabRun = new RunState("underdocks-punchoff-nab", new Overgrowth());
                var nabPlayer = Sts2Sim.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<Regent>(), nabRun);
                nabRun.AddPlayer(nabPlayer);
                var nabRoom = new EventRoom(() => (EventModel)ModelDb.Event<PunchOff>().MutableClone());
                nabRun.PushRoom(nabRoom);
                await nabRoom.Enter(nabRun);
                await nabRoom.Event.ChooseOption(nabRoom.Event.CurrentOptions.Single(option => option.Key == "NAB"));
                Assert.True(nabRoom.Event.TryDequeuePendingRewardOffer(out RewardsSet? nabRewards));
                RelicReward relicReward = Assert.Single(nabRewards!.ExtraRewards.OfType<RelicReward>());
                Assert.NotNull(relicReward.Relic);

                var deadRun = new RunState("underdocks-punchoff-nab-dead", new Overgrowth());
                var deadPlayer = Sts2Sim.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<Regent>(), deadRun);
                deadRun.AddPlayer(deadPlayer);
                var deadRoom = new EventRoom(() => (EventModel)ModelDb.Event<PunchOff>().MutableClone());
                deadRun.PushRoom(deadRoom);
                await deadRoom.Enter(deadRun);
                await CreatureCmd.Damage(deadRun, deadPlayer.Creature, deadPlayer.Creature.CurrentHp,
                    ValueProp.Unblockable | ValueProp.Unpowered);
                await deadRoom.Event.ChooseOption(deadRoom.Event.CurrentOptions.Single(option => option.Key == "NAB"));
                Assert.False(deadRoom.Event.TryDequeuePendingRewardOffer(out _));
                break;
            }
            case nameof(SpiralingWhirlpool):
            {
                await CreatureCmd.Damage(runState, player.Creature, 5m,
                    ValueProp.Unblockable | ValueProp.Unpowered);
                int damagedHp = player.Creature.CurrentHp;
                await eventModel.ChooseOption(eventModel.CurrentOptions.Single(option => option.Key == "DRINK"));
                Assert.True(player.Creature.CurrentHp > damagedHp);
                Assert.True(eventModel.IsFinished);
                break;
            }
        }
    }

    // 原版 Punch Off 是 Combat 布局事件：进房就 CreateCreature 两只 PunchConstruct 并抽 HP，选 Fight 复用它们，
    // 选 Nab 也已经抽过；额外奖励战后才掷骰（#46）。
    [Theory]
    [InlineData("nab")]
    [InlineData("fight-driver")]
    [InlineData("fight-engine")]
    public async Task PunchOff_PrecreatesEnemiesOnEntryLikeNative(string scenario)
    {
        var run = new RunState($"punchoff-precreate-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var probe = (PunchOffCombatStartProbe)new PunchOffCombatStartProbe().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        var room = new EventRoom(() => (EventModel)ModelDb.Event<PunchOff>().MutableClone());
        run.PushRoom(room);

        EncounterDefinition encounter = PunchOffEventEncounter.Definition;
        int[] expectedReductions = encounter
            .CreateMonsters(new Rng(RoomFactory.EncounterMonsterSeed(run.Rng.Seed, run.TotalFloor, encounter)))
            .Select(entry => ((PunchConstruct)entry.Monster).StartingHpReduction)
            .ToArray();
        int nicheBefore = run.Rng.Niche.Counter;
        var hpBounds = new List<(double Min, double Max)>();
        RngDrawObserver? previousObserver = RngDiagnostics.DrawObserver;
        try
        {
            RngDiagnostics.DrawObserver = (rng, op, min, max, arity) =>
            {
                if (ReferenceEquals(rng, run.Rng.Niche) && op == "NextInt" && arity == 2)
                    hpBounds.Add((min, max));
            };
            await room.Enter(run);
        }
        finally
        {
            RngDiagnostics.DrawObserver = previousObserver;
        }

        // PunchConstruct 的 A0 HP 区间只有 55：第二只的候选被第一只占满，原版走 NextInt(min, max + 1)。
        Assert.Equal(new[] { (0d, 1d), (55d, 56d) }, hpBounds);
        Assert.Equal(2, run.Rng.Niche.Counter - nicheBefore);
        var ev = Assert.IsType<PunchOff>(room.Event);
        Assert.Equal(new[] { "NAB", "I_CAN_TAKE_THEM" }, ev.CurrentOptions.Select(option => option.Key));

        if (scenario == "nab")
        {
            await ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == "NAB"));
            Assert.True(ev.IsFinished);
            Assert.False(ev.HasPendingForcedCombat);
            Assert.Null(probe.EnemiesAtStart);
            Assert.Equal(2, run.Rng.Niche.Counter - nicheBefore);
            return;
        }

        await CreatureCmd.SetMaxAndCurrentHp(player.Creature, 1_000_000_000m);
        await ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == "I_CAN_TAKE_THEM"));
        Assert.Equal("FIGHT", Assert.Single(ev.CurrentOptions).Key);
        probe.WatchExtraRewards(ev);
        int rewardsBefore = player.PlayerRng.Rewards.Counter;
        if (scenario == "fight-driver")
        {
            await new RunDriver(run, new PunchOffAttackDecisionSource()).DriveEventAsync(room);
        }
        else
        {
            await ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == "FIGHT"));
            await new RunEngine(run, points => points[0]).DriveEventAsync(room);
        }

        IReadOnlyList<Creature> enemies = Assert.IsAssignableFrom<IReadOnlyList<Creature>>(probe.EnemiesAtStart);
        Assert.Equal(new[] { 55, 55 }, enemies.Select(enemy => enemy.MaxHp));
        Assert.Equal(
            expectedReductions,
            enemies.Select(enemy => Assert.IsType<PunchConstruct>(enemy.Monster).StartingHpReduction));
        Assert.Equal(2, probe.NicheAtStart - nicheBefore);
        Assert.Equal(rewardsBefore, probe.RewardsAtFirstTurnEnd);
        Assert.False(probe.ExtrasPopulatedAtFirstTurnEnd);
        Assert.Equal(2, run.Rng.Niche.Counter - nicheBefore);
        Assert.True(ev.IsFinished);
    }
    private sealed class LastCardsSelectionSource : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>(request.Candidates
                .Skip(request.Candidates.Count - request.MaxCount)
                .Take(request.MaxCount)
                .ToArray());
    }
}
