using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
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

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class SovereignBladePowerInteractionTests : IDisposable
{
    public SovereignBladePowerInteractionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(VigorPower), typeof(DivineRight),
            typeof(SeekingEdgePower), typeof(ParryPower), typeof(WanderingGrunt), typeof(SovereignBlade),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task SeekingEdge_MakesSovereignBladeHitAllEnemiesWithoutTarget()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("seeking-edge");
        SovereignBlade blade = AddToHand(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        await PowerCmd.Apply<SeekingEdgePower>(
            room.Engine.State, player.Creature, 1m, player.Creature, null);

        await blade.PlayAsync(target: null);

        Assert.Equal(hpBefore - 10, enemy.CurrentHp);
    }

    [Fact]
    public async Task Parry_GrantsItsAmountAsBlockWhenSovereignBladeIsPlayed()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("parry");
        SovereignBlade blade = AddToHand(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<ParryPower>(
            room.Engine.State, player.Creature, 7m, player.Creature, null);

        await blade.PlayAsync(enemy);

        Assert.Equal(7, player.Creature.Block);
    }

    private static SovereignBlade AddToHand(Player player)
    {
        var blade = (SovereignBlade)ModelDb.Card<SovereignBlade>().MutableClone();
        blade.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(blade);
        player.PlayerCombatState.Energy = 3;
        return blade;
    }

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
