using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
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

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class PlatingPowerTests : IDisposable
{
    public PlatingPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(PlatingPower),
            typeof(DivineRight), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task PlayerPlating_GainsBlockAtTurnEnd_ThenDecrementsNextTurn()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync();
        PlatingPower plating = (await PowerCmd.Apply<PlatingPower>(
            room.Engine.State, player.Creature, 3m, player.Creature, null))!;

        await Hook.BeforeSideTurnEndEarly(
            room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(3, player.Creature.Block);

        await Hook.AfterSideTurnStart(
            room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(2, plating.Amount);
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync()
    {
        var runState = new RunState("plating", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        player.PlayerCombatState!.TurnNumber = 2;
        return (player, room);
    }
}
