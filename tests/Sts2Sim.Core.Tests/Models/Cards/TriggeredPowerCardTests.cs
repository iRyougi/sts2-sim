using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
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
public sealed class TriggeredPowerCardTests : IDisposable
{
    public TriggeredPowerCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Automation), typeof(Genesis), typeof(RollingBoulder),
            typeof(AutomationPower), typeof(GenesisPower), typeof(RollingBoulderPower),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(HardyBrute),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 2)]
    [InlineData(true, 3)]
    public async Task Genesis_GrantsStarsAfterOwnerEnergyReset(bool upgraded, int expected)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"genesis-{upgraded}");
        Genesis card = AddToHand<Genesis>(player);
        if (upgraded) card.Upgrade();
        await card.PlayAsync(target: null);

        int starsBefore = player.PlayerCombatState!.Stars;
        await Hook.AfterEnergyReset(room.Engine.State, player);

        Assert.Equal(starsBefore + expected, player.PlayerCombatState.Stars);

        await Hook.AfterEnergyReset(room.Engine.State, player);
        Assert.Equal(starsBefore + expected * 2, player.PlayerCombatState.Stars);

        Genesis second = AddToHand<Genesis>(player);
        if (upgraded) second.Upgrade();
        await second.PlayAsync(target: null);
        Assert.Equal(expected * 2, player.Creature.GetPower<GenesisPower>()!.Amount);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task Automation_InstancesTrackDrawsIndependentlyAndReset(bool upgraded, int expectedCost)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"automation-{upgraded}");
        Automation first = AddToHand<Automation>(player);
        if (upgraded) first.Upgrade();
        Assert.Equal(expectedCost, first.EnergyCost);
        await first.PlayAsync(target: null);

        player.PlayerCombatState!.Energy = 0;
        CardModel drawn = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        drawn.AssignOwner(player);
        for (int i = 0; i < 5; i++)
        {
            await Hook.AfterCardDrawn(room.Engine.State, drawn, fromHandDraw: false);
        }

        Automation second = AddToHand<Automation>(player);
        if (upgraded) second.Upgrade();
        await second.PlayAsync(target: null);
        player.PlayerCombatState.Energy = 0;
        Assert.Equal(2, player.Creature.Powers.OfType<AutomationPower>().Count());

        for (int batch = 1; batch <= 3; batch++)
        {
            for (int i = 0; i < 5; i++)
            {
                await Hook.AfterCardDrawn(room.Engine.State, drawn, fromHandDraw: false);
            }
            Assert.Equal(batch, player.PlayerCombatState.Energy);
        }
    }
    [Theory]
    [InlineData(false, 5, 10)]
    [InlineData(true, 10, 15)]
    public async Task RollingBoulder_DamagesEnemiesThenGrows(
        bool upgraded,
        int expectedDamage,
        int expectedNextDamage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"rolling-{upgraded}");
        RollingBoulder card = AddToHand<RollingBoulder>(player);
        if (upgraded) card.Upgrade();
        await card.PlayAsync(target: null);

        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;
        await Hook.AfterPlayerTurnStart(room.Engine.State, player);

        Assert.Equal(hpBefore - expectedDamage, room.Engine.State.Enemies.Single().CurrentHp);
        Assert.Equal(expectedNextDamage, player.Creature.GetPower<RollingBoulderPower>()!.Amount);

        await Hook.AfterPlayerTurnStart(room.Engine.State, player);
        Assert.Equal(hpBefore - expectedDamage - expectedNextDamage, room.Engine.State.Enemies.Single().CurrentHp);
        Assert.Equal(expectedNextDamage + 5, player.Creature.GetPower<RollingBoulderPower>()!.Amount);
    }

    [Fact]
    public async Task RollingBoulder_InstancesGrowIndependently()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("rolling-instances");
        await AddToHand<RollingBoulder>(player).PlayAsync(target: null);
        await Hook.AfterPlayerTurnStart(room.Engine.State, player);

        RollingBoulder upgraded = AddToHand<RollingBoulder>(player);
        upgraded.Upgrade();
        await upgraded.PlayAsync(target: null);
        Assert.Equal(2, player.Creature.Powers.OfType<RollingBoulderPower>().Count());

        int hpBefore = room.Engine.State.Enemies.Single().CurrentHp;
        await Hook.AfterPlayerTurnStart(room.Engine.State, player);
        Assert.Equal(hpBefore - 20, room.Engine.State.Enemies.Single().CurrentHp);
        Assert.Equal(new[] { 15, 15 }, player.Creature.Powers.OfType<RollingBoulderPower>().Select(p => p.Amount).ToArray());
    }
    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        player.PlayerCombatState.Energy = 10;
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
}
