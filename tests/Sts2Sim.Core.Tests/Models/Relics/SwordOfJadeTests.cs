using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class SwordOfJadeTests : IDisposable
{
    public SwordOfJadeTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EnterEveryCombatRoom_AppliesThreeStrength()
    {
        (var player, var firstRoom) = await Task11CombatTestSupport.CreateCombatAsync(
            "task11-sword-jade");
        await RelicCmd.Obtain(ModelDb.Relic<SwordOfJade>(), player);
        var runState = (Sts2Sim.Core.Runs.RunState)firstRoom.Engine.State.RunState;
        var secondRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

        await secondRoom.Enter(runState);
        Assert.Equal(3, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
        await secondRoom.Exit(runState);

        var thirdRoom = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await thirdRoom.Enter(runState);

        Assert.Equal(3, Assert.Single(player.Creature.Powers.OfType<StrengthPower>()).Amount);
        Assert.Equal(RelicRarity.Event, ModelDb.Relic<SwordOfJade>().Rarity);
    }
}
