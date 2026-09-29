namespace Sts2Sim.Core.Tests.Combat;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class OrbBehaviorTests
{
    public OrbBehaviorTests() => OrbFixture.InitModels();

    [Theory]
    [InlineData(typeof(LightningOrb), 3, 8, 5, 10)]
    [InlineData(typeof(FrostOrb), 2, 5, 4, 7)]
    [InlineData(typeof(DarkOrb), 6, 6, 8, 6)]
    [InlineData(typeof(PlasmaOrb), 1, 2, 1, 2)]
    [InlineData(typeof(GlassOrb), 4, 8, 6, 12)]
    public async Task PassiveAndEvoke_MatchSource(
        Type orbType, int basePassive, int baseEvoke, int focusedPassive, int focusedEvoke)
    {
        (CombatState state, _, Player player, Creature enemy, _) =
            await OrbFixture.Start($"orb-values-{orbType.Name}");
        OrbModel orb = (OrbModel)ModelDb.Get(orbType).MutableClone();
        await OrbCmd.Channel(state, orb, player);

        Assert.Equal(basePassive, orb.PassiveVal);
        Assert.Equal(baseEvoke, orb.EvokeVal);
        await PowerCmd.Apply<FocusPower>(state, player.Creature, 2m, player.Creature, null);
        Assert.Equal(focusedPassive, orb.PassiveVal);
        Assert.Equal(focusedEvoke, orb.EvokeVal);

        int oldEnergy = player.PlayerCombatState!.Energy;
        await OrbCmd.Passive(state, orb);
        switch (orb)
        {
            case LightningOrb:
                Assert.Equal(20 - focusedPassive, enemy.CurrentHp);
                break;
            case FrostOrb:
                Assert.Equal(focusedPassive, player.Creature.Block);
                break;
            case DarkOrb dark:
                Assert.Equal(baseEvoke + focusedPassive, dark.EvokeVal);
                break;
            case PlasmaOrb:
                Assert.Equal(oldEnergy + focusedPassive, player.PlayerCombatState.Energy);
                break;
            case GlassOrb glass:
                Assert.Equal(20 - focusedPassive, enemy.CurrentHp);
                Assert.Equal(3m, glass.RawPassiveValue);
                Assert.Equal(10m, glass.EvokeVal);
                break;
        }

        await OrbCmd.EvokeNext(state, player);
        Assert.Empty(player.PlayerCombatState.OrbQueue.Orbs);
        Assert.True(orb.HasBeenRemovedFromState);
        switch (orb)
        {
            case LightningOrb:
                Assert.Equal(20 - focusedPassive - focusedEvoke, enemy.CurrentHp);
                break;
            case FrostOrb:
                Assert.Equal(focusedPassive + focusedEvoke, player.Creature.Block);
                break;
            case DarkOrb dark:
                Assert.Equal(20 - dark.EvokeVal, enemy.CurrentHp);
                break;
            case PlasmaOrb:
                Assert.Equal(oldEnergy + focusedPassive + focusedEvoke, player.PlayerCombatState.Energy);
                break;
            case GlassOrb:
                Assert.Equal(20 - focusedPassive - 10, enemy.CurrentHp);
                break;
        }
    }

    [Fact]
    public async Task DamageHistoryAndRng_MatchSource()
    {
        (CombatState state, _, Player player, Creature enemy, _) =
            await OrbFixture.Start("orb-rng-history");
        var lightning = (LightningOrb)ModelDb.Orb<LightningOrb>().ToMutable();
        await OrbCmd.Channel(state, lightning, player);
        int before = state.RunState.Rng.CombatTargets.Counter;
        enemy.SetCurrentHpInternal(0);
        await lightning.Passive(state, null);
        Assert.Equal(before, state.RunState.Rng.CombatTargets.Counter);

        enemy.SetCurrentHpInternal(20);
        await lightning.Passive(state, null);
        Assert.Equal(before + 1, state.RunState.Rng.CombatTargets.Counter);
        Assert.Equal(17, enemy.CurrentHp);
        CombatDamageHistoryEntry entry = Assert.Single(state.DamageHistory.Entries);
        Assert.Same(player.Creature, entry.Dealer);
        Assert.Null(entry.CardSource);
        Assert.Equal(ValueProp.Unpowered, entry.Result.Props);
        Assert.Equal(1, state.SemanticHistory.CountOrbsChanneledThisCombat<LightningOrb>(state, player));
    }
}

[Collection("ModelDb")]
public sealed class OrbQueueTests
{
    public OrbQueueTests() => OrbFixture.InitModels();

    [Fact]
    public async Task ChannelEvokeCapacity_MatchSource()
    {
        var probe = new OrbFixture.Probe();
        (CombatState state, _, Player player, Creature enemy, _) =
            await OrbFixture.Start("orb-capacity", probe);
        var queue = player.PlayerCombatState!.OrbQueue;
        await OrbCmd.Channel<LightningOrb>(state, player);
        Assert.Equal(1, queue.Capacity); // Non-Defect's first channel adds one slot.
        await OrbCmd.Channel<FrostOrb>(state, player);
        Assert.Equal(12, enemy.CurrentHp);
        Assert.IsType<FrostOrb>(Assert.Single(queue.Orbs));
        Assert.Equal(new[] { "channel:LightningOrb", "evoke:LightningOrb", "channel:FrostOrb" },
            probe.Events.Where(x => x.StartsWith("channel:") || x.StartsWith("evoke:")));

        await OrbCmd.AddSlots(player, 12);
        Assert.Equal(10, queue.Capacity);
        await OrbCmd.Channel<DarkOrb>(state, player);
        await OrbCmd.EvokeLast(state, player, dequeue: false);
        Assert.Equal(6, enemy.CurrentHp);
        Assert.Equal(2, queue.Orbs.Count);
        Assert.Equal(queue.Orbs, state.IterateHookListeners().OfType<OrbModel>());
        Assert.False(queue.Orbs[^1].HasBeenRemovedFromState);

        OrbCmd.RemoveSlots(player, 9);
        Assert.Equal(1, queue.Capacity);
        Assert.IsType<FrostOrb>(Assert.Single(queue.Orbs));
        Assert.Equal(6, enemy.CurrentHp); // Tail removal does not evoke.
        OrbCmd.RemoveSlots(player, 3);
        Assert.Equal(0, queue.Capacity);
        Assert.Empty(queue.Orbs);
    }

    [Fact]
    public async Task TurnHookOrder_MatchSource()
    {
        var probe = new OrbFixture.Probe();
        (CombatState state, CombatEngine engine, Player player, Creature enemy, _) =
            await OrbFixture.Start("orb-turn-order", probe);
        probe.Events.Clear();
        await OrbCmd.Channel<FrostOrb>(state, player);
        await OrbCmd.AddSlots(player, 2);
        await OrbCmd.Channel<PlasmaOrb>(state, player);
        var queue = player.PlayerCombatState!.OrbQueue;
        var frost = Assert.IsType<FrostOrb>(queue.Orbs[0]);
        var plasma = Assert.IsType<PlasmaOrb>(queue.Orbs[1]);
        bool added = false;
        frost.PassiveActivated += () =>
        {
            probe.Events.Add("frost");
            if (added) return;
            added = true;
            var lightning = (LightningOrb)ModelDb.Orb<LightningOrb>().ToMutable();
            lightning.Owner = player;
            lightning.PassiveActivated += () => probe.Events.Add("lightning");
            queue.TryEnqueue(lightning).GetAwaiter().GetResult();
        };
        plasma.PassiveActivated += () => probe.Events.Add("plasma");
        await PowerCmd.Apply<HibernatePower>(state, player.Creature, 2m, player.Creature, null);
        await CreatureCmd.Damage(state, [player.Creature], 1m,
            ValueProp.Unblockable, enemy, null, null);
        CombatDamageHistoryEntry previousTurnDamage = Assert.Single(state.DamageHistory.Entries);
        Assert.False(previousTurnDamage.HappenedLastPlayerTurn(player));

        await engine.EndPlayerTurnAsync();
        int end = probe.Events.IndexOf("before-end");
        int frostIndex = probe.Events.IndexOf("frost");
        int start = probe.Events.IndexOf("side-start");
        int plasmaIndex = probe.Events.IndexOf("plasma");
        int autoplay = probe.Events.IndexOf("auto-preplay");
        Assert.True(end >= 0 && end < frostIndex && frostIndex < start &&
            start < plasmaIndex && plasmaIndex < autoplay,
            string.Join(',', probe.Events));
        Assert.DoesNotContain("lightning", probe.Events); // Added after the end-turn snapshot.
        Assert.True(previousTurnDamage.HappenedLastPlayerTurn(player));
        Assert.Equal(1, player.Creature.GetPower<HibernatePower>()!.Amount);
        Assert.Equal(4, player.PlayerCombatState.Energy); // 3 reset, then Plasma +1.

        await engine.EndPlayerTurnAsync();
        Assert.Contains("lightning", probe.Events);
    }

    [Fact]
    public async Task CloneDescriptionAndDeath_MatchSource()
    {
        (CombatState state, _, Player player, Creature enemy, _) =
            await OrbFixture.Start("orb-clone-death");
        await OrbCmd.AddSlots(player, 2);
        await OrbCmd.Channel<DarkOrb>(state, player);
        await OrbCmd.Channel<GlassOrb>(state, player);
        var dark = Assert.IsType<DarkOrb>(player.PlayerCombatState!.OrbQueue.Orbs[0]);
        var glass = Assert.IsType<GlassOrb>(player.PlayerCombatState.OrbQueue.Orbs[1]);
        await dark.Passive(state, null);
        enemy.GainBlockInternal(10m);
        await glass.Passive(state, null);
        Assert.True(Assert.Single(state.DamageHistory.Entries).Result.WasFullyBlocked);
        CardModel originalCard = player.PlayerCombatState.Hand.Cards[0];
        originalCard.AddEnergyCostThisCombat(2);
        originalCard.AddEnergyCostUntilPlayed(-1);

        CombatState clone = state.Clone();
        Player clonePlayer = clone.Players[0];
        var cloneDark = Assert.IsType<DarkOrb>(clonePlayer.PlayerCombatState!.OrbQueue.Orbs[0]);
        var cloneGlass = Assert.IsType<GlassOrb>(clonePlayer.PlayerCombatState.OrbQueue.Orbs[1]);
        Assert.Equal(2, clonePlayer.PlayerCombatState.OrbQueue.Capacity);
        Assert.Same(clonePlayer, cloneDark.Owner);
        Assert.Same(clonePlayer, cloneGlass.Owner);
        Assert.NotSame(dark, cloneDark);
        Assert.Equal(12m, cloneDark.EvokeVal);
        Assert.Equal(3m, cloneGlass.RawPassiveValue);
        Assert.Equal(OrbFixture.Digest(state), OrbFixture.Digest(clone));
        Assert.True(Assert.Single(clone.DamageHistory.Entries).Result.WasFullyBlocked);
        CardModel clonedCard = clonePlayer.PlayerCombatState.Hand.Cards[0];
        Assert.Equal(originalCard.LocalEnergyCost, clonedCard.LocalEnergyCost);
        clonedCard.AddEnergyCostUntilPlayed(1);
        Assert.NotEqual(originalCard.LocalEnergyCost, clonedCard.LocalEnergyCost);

        await cloneDark.Passive(clone, null);
        await cloneGlass.Passive(clone, null);
        Assert.Equal(12m, dark.EvokeVal);
        Assert.Equal(3m, glass.RawPassiveValue);
        Assert.Equal(18m, cloneDark.EvokeVal);
        Assert.Equal(2m, cloneGlass.RawPassiveValue);
        Assert.NotEqual(OrbFixture.Digest(state), OrbFixture.Digest(clone));

        await CreatureCmd.Damage(state, [player.Creature], 100m,
            ValueProp.Unblockable | ValueProp.Unpowered, enemy, null, null);
        Assert.True(player.Creature.IsDead);
        Assert.Empty(player.PlayerCombatState.OrbQueue.Orbs);
        Assert.Equal(0, player.PlayerCombatState.OrbQueue.Capacity);
        Assert.Equal(2, clonePlayer.PlayerCombatState.OrbQueue.Orbs.Count);
    }
}

internal static class OrbFixture
{
    public static void InitModels()
    {
        ModelDb.ResetForTests();
        ModelDb.Init([
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent),
            typeof(FallingStar), typeof(Venerate), typeof(DivineRight),
            typeof(TrainingDummy), typeof(WeakPower), typeof(VulnerablePower),
            typeof(MinionPower), typeof(FocusPower), typeof(HibernatePower),
            typeof(LightningOrb), typeof(FrostOrb), typeof(DarkOrb),
            typeof(PlasmaOrb), typeof(GlassOrb),
        ]);
    }

    public static async Task<(CombatState State, CombatEngine Engine, Player Player,
        Creature Enemy, RunState Run)> Start(string seed, Probe? probe = null)
    {
        var run = new RunState(seed, probe);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var state = new CombatState(run);
        Creature enemy = state.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
        var engine = new CombatEngine(state);
        await engine.StartCombatAsync();
        return (state, engine, player, enemy, run);
    }

    public static CombatStateDescriptionDigest Digest(CombatState state)
    {
        var builder = new CombatStateDescriptionBuilder();
        CombatStateDescription.AppendExactState(ref builder, state);
        return builder.Build();
    }

    internal sealed class RunState(string seed, Probe? probe) : IRunState
    {
        private readonly List<Player> _players = [];
        public RunRngSet Rng { get; } = new(seed);
        public AscensionManager Ascension { get; } = new(0);
        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;
        public AbstractRoom? CurrentRoom => null;
        public void AddPlayer(Player player) => _players.Add(player);
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            (probe is null ? [] : new AbstractModel[] { probe })
                .Concat(childCombatState?.IterateHookListeners() ?? []);
    }

    internal sealed class Probe : AbstractModel
    {
        public List<string> Events { get; } = [];
        public override bool ShouldReceiveCombatHooks => true;
        public override Task AfterOrbChanneled(Player player, OrbModel orb)
        {
            Events.Add($"channel:{orb.GetType().Name}");
            return Task.CompletedTask;
        }
        public override Task AfterOrbEvoked(OrbModel orb, IEnumerable<Creature> targets)
        {
            Events.Add($"evoke:{orb.GetType().Name}");
            return Task.CompletedTask;
        }
        public override Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
        {
            if (side == CombatSide.Player) Events.Add("before-end");
            return Task.CompletedTask;
        }
        public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
        {
            if (side == CombatSide.Player) Events.Add("side-start");
            return Task.CompletedTask;
        }
        public override Task AfterAutoPrePlayPhaseEntered(Player player)
        {
            Events.Add("auto-preplay");
            return Task.CompletedTask;
        }
    }
}
