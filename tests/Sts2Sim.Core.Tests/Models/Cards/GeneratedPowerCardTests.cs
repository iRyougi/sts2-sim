using Sts2Sim.Core.Content.Acts;
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
public sealed class GeneratedPowerCardTests : IDisposable
{
    public GeneratedPowerCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
            typeof(BlackHole), typeof(BlackHolePower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task BlackHole_AppliesExpectedPowerAmount()
    {
        var runState = new RunState("black-hole", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        var card = (BlackHole)ModelDb.Card<BlackHole>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);

        await card.PlayAsync(target: null);

        Assert.Equal(3, player.Creature.Powers.OfType<BlackHolePower>().Single().Amount);
    }
}
