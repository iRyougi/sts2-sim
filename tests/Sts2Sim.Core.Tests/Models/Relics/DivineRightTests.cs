using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public class DivineRightTests : IDisposable
{
    public DivineRightTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight), typeof(Circlet), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task NewRun_StartsWithDivineRight_AndGrantsStarsOnCombatRoomEntered()
    {
        var runState = new RunState("divine-right-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        Assert.Single(player.Relics);
        Assert.IsType<DivineRight>(player.Relics[0]);

        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);

        Assert.Equal(3, player.PlayerCombatState!.Stars);
    }

    [Fact]
    public async Task NewRun_DivineRight_DoesNotGrantStars_OnNonCombatRoom()
    {
        var runState = new RunState("divine-right-b", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);

        var room = new TreasureRoom(goldAmount: 0);
        await room.Enter(runState);

        Assert.Null(player.PlayerCombatState);
    }
}



