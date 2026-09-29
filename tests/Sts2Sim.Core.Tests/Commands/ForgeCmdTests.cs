using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Commands;

[Collection("ModelDb")]
public sealed class ForgeCmdTests : IDisposable
{
    public ForgeCmdTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
            typeof(SovereignBlade),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Forge_CreatesOneBladeThenRefinesIt()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("forge-refine");

        await ForgeCmd.Forge(5m, player, source: null);
        await ForgeCmd.Forge(3m, player, source: null);

        SovereignBlade blade = Assert.Single(AllBlades(player));
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        player.PlayerCombatState!.Energy = 3;
        await blade.PlayAsync(enemy);
        Assert.Equal(hpBefore - 18, enemy.CurrentHp);
    }

    [Fact]
    public async Task Forge_WithOnlyExhaustedBlade_CreatesNewBladeAndRefinesBoth()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("forge-exhausted");
        await ForgeCmd.Forge(5m, player, source: null);
        SovereignBlade exhausted = Assert.Single(AllBlades(player));
        CardPileCmd.Add(exhausted, PileType.Exhaust);

        await ForgeCmd.Forge(3m, player, source: null);

        SovereignBlade[] blades = AllBlades(player).ToArray();
        Assert.Equal(2, blades.Length);
        SovereignBlade fresh = Assert.Single(blades, blade => !ReferenceEquals(blade, exhausted));
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        CardPileCmd.Add(exhausted, PileType.Hand);
        player.PlayerCombatState!.Energy = 3;
        await exhausted.PlayAsync(enemy);
        player.PlayerCombatState.Energy = 3;
        await fresh.PlayAsync(enemy);
        Assert.Equal(hpBefore - 31, enemy.CurrentHp);
    }

    private static IEnumerable<SovereignBlade> AllBlades(Player player) =>
        player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).OfType<SovereignBlade>();

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

