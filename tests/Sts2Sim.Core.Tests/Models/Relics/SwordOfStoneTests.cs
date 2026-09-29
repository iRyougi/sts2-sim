using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class SwordOfStoneTests : IDisposable
{
    public SwordOfStoneTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task FifthEliteVictory_ReplacesAtSameIndex_AndNormalVictoryDoesNotCount()
    {
        (var player, var eliteRoom) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-sword-stone");
        await RelicCmd.Obtain(ModelDb.Relic<SwordOfStone>(), player);
        SwordOfStone stone = Assert.Single(player.Relics.OfType<SwordOfStone>());
        int index = player.Relics.ToList().IndexOf(stone);
        var runState = (Sts2Sim.Core.Runs.RunState)eliteRoom.Engine.State.RunState;

        var normalRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            RoomType.Monster);
        runState.PushRoom(normalRoom);
        await Hook.AfterCombatVictory(eliteRoom.Engine.State);
        Assert.Same(normalRoom, runState.PopCurrentRoom());
        Assert.Contains(stone, player.Relics);

        var eliteMarker = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            RoomType.Elite);
        runState.PushRoom(eliteMarker);
        for (int victory = 1; victory <= 4; victory++)
        {
            await Hook.AfterCombatVictory(eliteRoom.Engine.State);
            Assert.Contains(stone, player.Relics);
        }
        await Hook.AfterCombatVictory(eliteRoom.Engine.State);

        Assert.IsType<SwordOfJade>(player.Relics[index]);
        Assert.DoesNotContain(stone, player.Relics);
    }

    [Fact]
    public void Metadata_IsEventRarity() =>
        Assert.Equal(RelicRarity.Event, ModelDb.Relic<SwordOfStone>().Rarity);

}
