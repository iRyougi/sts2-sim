using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

file sealed class SpyRelic : RelicModel
{
    public int RoomsEntered { get; private set; }

    public int CombatStarts { get; private set; }

    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        RoomsEntered++;
        return Task.CompletedTask;
    }

    public override Task BeforeCombatStart()
    {
        CombatStarts++;
        return Task.CompletedTask;
    }
}

file sealed class CombatSpyPower : PowerModel
{
    public int CombatStarts { get; private set; }

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCombatStart()
    {
        CombatStarts++;
        return Task.CompletedTask;
    }
}

public class RelicModelTests : IDisposable
{
    public RelicModelTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight), typeof(Sts2Sim.Core.Models.Relics.Circlet), typeof(SpyRelic), typeof(CombatSpyPower) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task RelicCmd_Obtain_AddsToPlayerRelicsAndFiresHooksGoingForward()
    {
        var runState = new RunState("relic-model-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        await RelicCmd.Obtain(ModelDb.Relic<SpyRelic>(), player);

        Assert.Equal(2, player.Relics.Count);
        var spy = Assert.Single(player.Relics.OfType<SpyRelic>());
        Assert.True(spy.IsMutable);

        var room = new TreasureRoom(goldAmount: 0);
        await room.Enter(runState);

        Assert.Equal(1, spy.RoomsEntered);
    }

    [Fact]
    public async Task RelicCmd_Obtain_ClonesAssignsOwnerAndReceivesCombatHooksOnceAlongsideCombatListeners()
    {
        var runState = new RunState("relic-model-combat", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        SpyRelic canonical = ModelDb.Relic<SpyRelic>();

        await RelicCmd.Obtain(canonical, player);

        Assert.Equal(2, player.Relics.Count);
        var relic = Assert.Single(player.Relics.OfType<SpyRelic>());
        Assert.NotSame(canonical, relic);
        Assert.Same(player, relic.Owner);

        var combatState = new CombatState(runState);
        player.ResetCombatState();
        combatState.AddPlayerCreature(player.Creature);
        CombatSpyPower combatListener = (await PowerCmd.Apply<CombatSpyPower>(combatState, player.Creature, 1m, null, null))!;

        await Hook.BeforeCombatStart(combatState);

        Assert.Equal(1, relic.CombatStarts);
        Assert.Equal(1, combatListener.CombatStarts);
    }
}
