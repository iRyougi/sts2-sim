using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

file sealed class DenseDoubleRestHealRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override decimal ModifyRestSiteHealAmount(Creature creature, decimal amount) =>
        ReferenceEquals(creature, Owner.Creature) ? amount * 2m : amount;
}

[Collection("ModelDb")]
public sealed class DenseVegetationTests : IDisposable
{
    public DenseVegetationTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(DenseVegetation),
            typeof(Wriggler),
            typeof(Infection),
            typeof(StrengthPower),
            typeof(DenseDoubleRestHealRelic),
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public async Task IsAllowed_AllowsLowHpSoloRunsButRejectsMultiplayerRunsWithAnyPlayerAtEightHp()
    {
        var runState = new RunState("dense-is-allowed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        DenseVegetation vegetation = ModelDb.Event<DenseVegetation>();
        await CreatureCmd.LoseHp(
            runState,
            player.Creature,
            player.Creature.CurrentHp - 8m,
            Sts2Sim.Core.ValueProps.ValueProp.Unblockable);

        Assert.True(vegetation.IsAllowed(runState));

        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(other);

        Assert.False(vegetation.IsAllowed(runState));
    }

    [Fact]
    public async Task IsAllowed_AllowsMultiplayerRunsWhenEveryPlayerHasAtLeastNineHp()
    {
        var runState = new RunState("dense-is-allowed-nine-hp", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.AddPlayer(other);
        DenseVegetation vegetation = ModelDb.Event<DenseVegetation>();

        await CreatureCmd.LoseHp(
            runState,
            player.Creature,
            player.Creature.CurrentHp - 9m,
            Sts2Sim.Core.ValueProps.ValueProp.Unblockable);

        Assert.True(vegetation.IsAllowed(runState));

        await CreatureCmd.LoseHp(runState, player.Creature, 1m, Sts2Sim.Core.ValueProps.ValueProp.Unblockable);

        Assert.False(vegetation.IsAllowed(runState));
    }
    [Fact]
    public async Task TrudgeOn_DealsEightUnblockableUnpoweredDamage_AndGoldCanReachSixtyOneThroughNinetyNine()
    {
        var observed = new HashSet<int>();
        for (int i = 0; i < 2_048 && (!observed.Contains(61) || !observed.Contains(99)); i++)
        {
            (_, Player player, DenseVegetation ev) = Setup($"dense-gold-{i}");
            player.Gold = 0;
            player.Creature.GainBlockInternal(20m);
            int hpBefore = player.Creature.CurrentHp;

            await Choose(ev, "TRUDGE_ON");

            observed.Add(player.Gold);
            Assert.Equal(hpBefore - 8, player.Creature.CurrentHp);
            Assert.Equal(20, player.Creature.Block);
            Assert.True(ev.IsFinished);
        }

        Assert.All(observed, gold => Assert.InRange(gold, 61, 99));
        Assert.Contains(61, observed);
        Assert.Contains(99, observed);
    }

    [Fact]
    public async Task Rest_UsesThirtyPercentRestSiteHealWithModifiers_ThenFlipsToFightPage()
    {
        (RunState runState, Player player, DenseVegetation ev) = Setup("dense-rest");
        await RelicCmd.Obtain(ModelDb.Relic<DenseDoubleRestHealRelic>(), player);
        await CreatureCmd.LoseHp(runState, player.Creature, 60m, Sts2Sim.Core.ValueProps.ValueProp.Unblockable);
        int hpBefore = player.Creature.CurrentHp;

        await Choose(ev, "REST");

        Assert.Equal(hpBefore + (int)(player.Creature.MaxHp * 0.6m), player.Creature.CurrentHp);
        Assert.False(ev.IsFinished);
        Assert.Equal("FIGHT", Assert.Single(ev.CurrentOptions).Key);
    }

    [Fact]
    public async Task Fight_RequestsExactlyFourWrigglers_AndFinishesWithoutResumeState()
    {
        (_, _, DenseVegetation ev) = Setup("dense-fight");
        await Choose(ev, "REST");

        await Choose(ev, "FIGHT");

        Assert.True(ev.TryDequeuePendingForcedCombatSlottedBatch(out var factory));
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> slotted = factory();
        // Native DenseVegetationEventEncounter slots; Wriggler's opening move reads them (#185).
        Assert.Equal(
            new[] { "wriggler1", "wriggler2", "wriggler3", "wriggler4" },
            slotted.Select(entry => entry.SlotName));
        IReadOnlyList<MonsterModel> monsters = slotted.Select(entry => entry.Monster).ToList();
        Assert.Equal(4, monsters.Count);
        Assert.All(monsters, monster => Assert.IsType<Wriggler>(monster));
        Assert.Equal(4, monsters.Distinct(ReferenceEqualityComparer.Instance).Count());
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, DenseVegetation Event) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var ev = (DenseVegetation)ModelDb.Event<DenseVegetation>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(DenseVegetation ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
