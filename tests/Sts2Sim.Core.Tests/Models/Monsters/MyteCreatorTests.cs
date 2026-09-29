namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
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

[Collection("ModelDb")]
public sealed class MyteCreatorTests : IDisposable
{
    public MyteCreatorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task EnemyToxicHasNoCreatorForRegentRelicAndArsenal()
    {
        const string seed = "09-s7-131c-myte-creator";
        var run = new RunState(seed, new Overgrowth(), 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        await RelicCmd.Obtain(ModelDb.Relic<Regalite>(), player);
        var room = new CombatRoom(() => (Myte)ModelDb.Monster<Myte>().MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        await PowerCmd.Apply<ArsenalPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);
        Myte myte = Assert.IsType<Myte>(Assert.Single(room.Engine.State.Enemies).Monster);
        Assert.Equal("TOXIC_MOVE", myte.NextMove?.StateId);

        await myte.PerformMove();

        int toxicCount = player.PlayerCombatState!.Hand.Cards.OfType<Toxic>().Count();
        int block = player.Creature.Block;
        int strength = player.Creature.GetPower<StrengthPower>()?.Amount ?? 0;
        Assert.True(toxicCount == 2 && block == 0 && strength == 0,
            $"seed={seed}; Toxic={toxicCount}, Block={block}, Strength={strength}; expected 2,0,0 for null creator");
    }
}
