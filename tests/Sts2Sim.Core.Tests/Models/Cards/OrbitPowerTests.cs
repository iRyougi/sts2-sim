using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class OrbitPowerTests : IDisposable
{
    private sealed class TwoCostSkill : CardModel
    {
        public override CardType Type => CardType.Skill;
        public override CardRarity Rarity => CardRarity.Basic;
        public override TargetType TargetType => TargetType.Self;
        protected override int CanonicalEnergyCost => 2;
    }
    private sealed class FourCostSkill : CardModel
    {
        public override CardType Type => CardType.Skill;
        public override CardRarity Rarity => CardRarity.Basic;
        public override TargetType TargetType => TargetType.Self;
        protected override int CanonicalEnergyCost => 4;
    }

    private sealed class EightCostSkill : CardModel
    {
        public override CardType Type => CardType.Skill;
        public override CardRarity Rarity => CardRarity.Basic;
        public override TargetType TargetType => TargetType.Self;
        protected override int CanonicalEnergyCost => 8;
    }

    public OrbitPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Orbit), typeof(OrbitPower), typeof(RollingBoulder), typeof(RollingBoulderPower),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(HardyBrute),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 1)]
    public async Task Orbit_AppliesOnePower_AndUpgradeReducesEnergyCost(bool upgraded, int expectedCost)
    {
        (Player player, _) = await CreateCombatAsync($"orbit-cost-{upgraded}");
        Orbit orbit = AddToHand<Orbit>(player, expectedCost);
        if (upgraded)
        {
            orbit.Upgrade();
        }

        Assert.Equal(expectedCost, orbit.EnergyCost);
        await orbit.PlayAsync(target: null);

        OrbitPower power = Assert.Single(player.Creature.Powers.OfType<OrbitPower>());
        Assert.Equal(1, power.Amount);
        Assert.Equal(PowerInstanceType.Instanced, power.InstanceType);
    }

    [Fact]
    public async Task Orbit_GrantsEnergyOnlyAfterCumulativeFourEnergyIsSpent()
    {
        (Player player, _) = await CreateCombatAsync("orbit-four-energy");
        await AddToHand<Orbit>(player, 2).PlayAsync(target: null);

        await AddToHand<RollingBoulder>(player, 3).PlayAsync(target: null);
        Assert.Equal(0, player.PlayerCombatState!.Energy);

        await AddToHand<DefendRegent>(player, 1).PlayAsync(target: null);
        Assert.Equal(1, player.PlayerCombatState!.Energy);
    }

    [Fact]
    public async Task Orbit_GrantsEnergyTwice_WhenOneCardSpendsEightEnergy()
    {
        (Player player, _) = await CreateCombatAsync("orbit-eight-energy");
        await AddToHand<Orbit>(player, 2).PlayAsync(target: null);

        await AddToHand((EightCostSkill)new EightCostSkill().MutableClone(), player, 8).PlayAsync(target: null);

        Assert.Equal(2, player.PlayerCombatState!.Energy);
    }

    [Fact]
    public async Task Orbit_InstancesKeepIndependentProgress_AndBothGrantEnergy()
    {
        (Player player, _) = await CreateCombatAsync("orbit-independent-progress");
        await AddToHand<Orbit>(player, 2).PlayAsync(target: null);

        await AddToHand((TwoCostSkill)new TwoCostSkill().MutableClone(), player, 2).PlayAsync(target: null);
        Assert.Equal(0, player.PlayerCombatState!.Energy);

        await AddToHand<Orbit>(player, 2).PlayAsync(target: null);
        Assert.Equal(1, player.PlayerCombatState!.Energy);

        await AddToHand((TwoCostSkill)new TwoCostSkill().MutableClone(), player, 2).PlayAsync(target: null);
        Assert.Equal(0, player.PlayerCombatState!.Energy);

        await AddToHand((TwoCostSkill)new TwoCostSkill().MutableClone(), player, 2).PlayAsync(target: null);
        Assert.Equal(2, player.PlayerCombatState!.Energy);
        Assert.Equal(2, player.Creature.Powers.OfType<OrbitPower>().Count());
    }

    [Fact]
    public async Task Orbit_IgnoresEnergySpentByAnotherPlayer()
    {
        (Player owner, Player other, _) = await CreateTwoPlayerCombatAsync("orbit-other-owner");
        await AddToHand<Orbit>(owner, 2).PlayAsync(target: null);

        await AddToHand((FourCostSkill)new FourCostSkill().MutableClone(), other, 4).PlayAsync(target: null);

        Assert.Equal(0, owner.PlayerCombatState!.Energy);
        Assert.Equal(0, other.PlayerCombatState!.Energy);
    }

    private static TCard AddToHand<TCard>(Player player, int energy)
        where TCard : CardModel
    {
        return AddToHand((TCard)ModelDb.Card<TCard>().MutableClone(), player, energy);
    }

    private static TCard AddToHand<TCard>(TCard card, Player player, int energy)
        where TCard : CardModel
    {
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        player.PlayerCombatState.Energy = energy;
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (HardyBrute)ModelDb.Monster<HardyBrute>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }

    private static async Task<(Player owner, Player other, CombatRoom room)> CreateTwoPlayerCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(other);
        var room = new CombatRoom(() => (HardyBrute)ModelDb.Monster<HardyBrute>().MutableClone());
        await room.Enter(runState);
        return (owner, other, room);
    }
}
