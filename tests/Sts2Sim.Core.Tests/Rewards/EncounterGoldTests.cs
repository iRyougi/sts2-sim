using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

[Collection("ModelDb")]
public sealed class EncounterGoldTests : IDisposable
{
    public EncounterGoldTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, false, false, null, null, 0, 100)]
    [InlineData(false, true, false, null, null, 0, 75)]
    [InlineData(true, false, true, null, null, 0, 100)]
    [InlineData(true, true, false, null, null, 0, 50)]
    [InlineData(true, true, true, null, null, 0, 0)]
    [InlineData(false, false, false, 300, 300, 0, 300)]
    [InlineData(false, true, false, 300, 300, 0, 225)]
    [InlineData(false, true, false, 10, 10, 0, 8)]
    [InlineData(false, true, false, null, null, 3, 56)]
    [InlineData(false, true, false, 300, 300, 3, 225)]
    [InlineData(false, true, false, 100, null, 0, 75)]
    [InlineData(false, true, false, null, 100, 0, 75)]
    public async Task CombatVictory_OffersEncounterGoldFromSpawnAndTheftHistory(
        bool gremlinMerc, bool escape, bool stealGold, int? minGold, int? maxGold,
        int ascension, int expectedGold)
    {
        var run = new RunState("encounter-gold-history", [new Overgrowth(), new Hive()], ascension);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        EncounterDefinition encounter = gremlinMerc
            ? UnderdocksEncounters.BatchA.Single(definition => definition.Name == "GremlinMercNormal")
            : new EncounterDefinition(() =>
                [(ModelDb.Monster<TrainingDummy>(), (string?)null),
                 (ModelDb.Monster<TrainingDummy>(), (string?)null)])
            {
                MinGoldReward = minGold,
                MaxGoldReward = maxGold,
            };
        // A non-final boss room gives a deterministic 100-gold baseline without overriding the reward.
        var room = new CombatRoom(() => (encounter, encounter.CreateMonsters()), RoomType.Boss);
        run.PushRoom(room);
        await room.EnterInternal(run);
        CombatState state = room.Engine.State;
        Creature escaping;
        if (gremlinMerc)
        {
            Creature merc = Assert.Single(state.Enemies);
            if (stealGold)
            {
                ThieveryPower theft = Assert.Single(merc.Powers.OfType<ThieveryPower>());
                await theft.Steal();
                Assert.Equal(20, theft.GoldStolen);
            }

            await CreatureCmd.Kill(merc);
            escaping = state.Enemies.Single(creature => creature.Monster is FatGremlin);
        }
        else
        {
            // Four enemies appeared; one is removed and one of the two live summons may escape.
            escaping = await CreatureCmd.Add(
                (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), state, CombatSide.Enemy, null);
            await CreatureCmd.Add(
                (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(), state, CombatSide.Enemy, null);
            Creature removed = state.Enemies[0];
            await CreatureCmd.Kill(removed);
            if (state.ContainsCreature(removed)) state.RemoveCreature(removed);
        }

        if (escape) await CreatureCmd.Escape(escaping);
        CombatState clone = state.Clone();
        Assert.Equal(gremlinMerc ? 3 : 4, clone.SpawnedEnemies.Count);
        Assert.All(clone.SpawnedEnemies, creature => Assert.DoesNotContain(creature, state.SpawnedEnemies));
        Assert.Equal(stealGold, clone.GoldWasStolen);
        Assert.Equal(stealGold, state.GoldWasStolen);
        foreach (Creature enemy in state.Enemies.Where(creature => creature.IsAlive).ToArray())
            await CreatureCmd.Kill(enemy);
        room.Engine.CheckWinCondition();
        await room.ResolveOutcomeAsync();

        Assert.True(room.Won);
        Assert.Equal(expectedGold, Assert.Single(room.GeneratedRewards).Gold.Amount);
        int goldBeforeTaking = player.Gold;
        await room.Rewards!.Gold.Take();
        Assert.Equal(goldBeforeTaking + expectedGold, player.Gold);
        Assert.Equal(goldBeforeTaking, clone.Players[0].Gold);

        // Encounter definitions are shared; the next combat must not inherit historical theft.
        await room.Exit(run);
        var nextRoom = new CombatRoom(() => (encounter, encounter.CreateMonsters()), RoomType.Boss);
        await nextRoom.EnterInternal(run);
        Assert.False(nextRoom.Engine.State.GoldWasStolen);
    }
}
