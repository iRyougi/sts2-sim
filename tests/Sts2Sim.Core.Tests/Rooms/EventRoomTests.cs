using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rooms;

public class EventRoomTests : IDisposable
{
    public EventRoomTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
            typeof(JungleMazeAdventure),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EnterInternal_BeginsEventBoundToCurrentPlayerAndRun()
    {
        var runState = new RunState("event-room-a", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new EventRoom(() => (JungleMazeAdventure)ModelDb.Event<JungleMazeAdventure>().MutableClone());

        await room.EnterInternal(runState);

        Assert.Same(player, room.Event.Owner);
        Assert.Same(runState, room.Event.RunState);
        Assert.False(room.Event.IsFinished);
        Assert.Equal(2, room.Event.CurrentOptions.Count);
    }
}
