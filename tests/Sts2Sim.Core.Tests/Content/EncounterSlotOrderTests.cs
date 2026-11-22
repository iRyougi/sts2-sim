using System.Reflection;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Content;

[Collection("ModelDb")]
public sealed class EncounterSlotOrderTests : IDisposable
{
    public EncounterSlotOrderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    // Game EncounterModel.Slots on FabricatorNormal / OvicopterNormal / TheObscuraNormal / PhrogParasiteElite;
    // CombatManager.AddCreature calls CombatState.SortEnemiesBySlotName for every added creature with a slot.
    [Theory]
    [InlineData("FabricatorNormal", "bot1,bot2,fabricator,bot3,bot4", "bot1", "fabricator")]
    [InlineData("OvicopterNormal", "egg1,egg2,egg3,egg4,egg5,ovicopter", "egg3,egg4,egg5,ovicopter", null)]
    [InlineData("TheObscuraNormal", "illusion,obscura", "illusion,obscura", null)]
    [InlineData("PhrogParasite", "phrog,wriggler1,wriggler2,wriggler3,wriggler4", "wriggler1,wriggler2,wriggler3,wriggler4", null)]
    public async Task SummonedMonsters_AreOrderedByTheEncountersSlotList(
        string encounterName, string slots, string expectedOrder, string? expectedLast)
    {
        EncounterDefinition encounter = FindEncounter(encounterName);
        Assert.Equal(slots.Split(','), encounter.Slots);

        var run = new RunState("encounter-slot-order", new Overgrowth(), 0);
        run.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), run));
        var room = new CombatRoom(() => (encounter, encounter.CreateMonsters()), RoomType.Monster);
        run.PushRoom(room);
        await room.Enter(run);

        if (encounterName == "PhrogParasite")
        {
            Creature phrog = Assert.Single(room.Engine.State.Enemies);
            await CreatureCmd.Damage(room.Engine.State, new[] { phrog }, phrog.CurrentHp,
                ValueProp.Unblockable | ValueProp.Unpowered, dealer: run.Players[0].Creature,
                cardSource: null, cardPlay: null);
        }
        else
        {
            await room.Engine.EndPlayerTurnAsync();
        }

        string[] actual = room.Engine.State.Enemies.Select(c => c.SlotName!).ToArray();
        int[] indexes = actual.Select(slot => encounter.Slots.ToList().IndexOf(slot)).ToArray();
        Assert.True(indexes.Zip(indexes.Skip(1), (a, b) => a < b).All(ok => ok) && indexes.All(i => i >= 0),
            $"{encounterName}: enemies must follow Slots order, got {string.Join(',', actual)}");
        if (expectedLast is null)
        {
            Assert.Equal(expectedOrder.Split(','), actual);
        }
        else
        {
            Assert.Equal(expectedOrder, actual[0]);
            Assert.Equal(expectedLast, actual[^1]);
        }
    }

    private static EncounterDefinition FindEncounter(string name)
    {
        var overgrowthElites = (IReadOnlyList<EncounterDefinition>)typeof(Overgrowth)
            .GetProperty("EliteEncounters", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(new Overgrowth())!;
        return GloryEncounters.BatchA.Concat(HiveEncounters.BatchA).Concat(HiveEncounters.BatchB)
            .Concat(overgrowthElites).Single(e => e.Name == name);
    }
}
