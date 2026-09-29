using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Content;

[Collection("ModelDb")]
public sealed class EncounterDefinitionTests : IDisposable
{
    public EncounterDefinitionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(TrainingDummy),
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CreateMonsters_ClonesIndependentMutableModelsInOrder_WithSlotsAndTags()
    {
        MonsterModel canonical = ModelDb.Monster<TrainingDummy>();
        var encounter = new EncounterDefinition(
            () => new (MonsterModel Monster, string? SlotName)[]
            {
                (canonical, "front"),
                (canonical, "rear"),
            },
            new[] { EncounterTag.Crawler, EncounterTag.Shrinker });

        IReadOnlyList<(MonsterModel Monster, string? SlotName)> first = encounter.CreateMonsters();
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> second = encounter.CreateMonsters();

        Assert.Equal(new[] { "front", "rear" }, first.Select(entry => entry.SlotName));
        Assert.Equal(new[] { EncounterTag.Crawler, EncounterTag.Shrinker }, encounter.Tags);
        Assert.All(first, entry => Assert.True(entry.Monster.IsMutable));
        Assert.NotSame(first[0].Monster, first[1].Monster);
        Assert.NotSame(first[0].Monster, second[0].Monster);
        Assert.NotSame(first[1].Monster, second[1].Monster);
    }

    [Fact]
    public async Task CombatRoom_EncounterBatchAssignsSlotsToEveryInitialCreature()
    {
        MonsterModel canonical = ModelDb.Monster<TrainingDummy>();
        var encounter = new EncounterDefinition(
            () => new (MonsterModel Monster, string? SlotName)[]
            {
                (canonical, "left"),
                (canonical, "right"),
            });
        var runState = new RunState("encounter-slots", new Overgrowth());
        runState.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Regent>(), runState));
        var room = new CombatRoom(encounter.CreateMonsters);
        Assert.Equal(CombatRoom.CustomEncounterName, room.EncounterName);
        runState.PushRoom(room);

        await room.Enter(runState);

        Assert.Equal(new[] { "left", "right" }, room.Engine.State.Enemies.Select(enemy => enemy.SlotName));
        Assert.Equal(new uint?[] { 1, 2 }, room.Engine.State.Enemies.Select(enemy => enemy.CombatId));
    }

    [Fact]
    public void SingleFactory_RemainsCompatibleAndCreatesOneMutableMonsterWithNoSlot()
    {
        var encounter = new EncounterDefinition(() => ModelDb.Monster<TrainingDummy>());

        MonsterModel first = encounter.CreateMonster();
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> second = encounter.CreateMonsters();

        Assert.True(first.IsMutable);
        Assert.Single(second);
        Assert.True(second[0].Monster.IsMutable);
        Assert.Null(second[0].SlotName);
        Assert.NotSame(first, second[0].Monster);
        Assert.Empty(encounter.Tags);
        Assert.Equal(EncounterDefinition.DefaultName, encounter.Name);
    }

    [Fact]
    public void ExplicitName_IsRetainedIndependentlyOfTheGeneratedMonsterBatch()
    {
        var encounter = new EncounterDefinition(
            monsterFactory: () => ModelDb.Monster<TrainingDummy>(),
            tags: null,
            isWeak: false,
            name: "TrainingDummyIntro");

        IReadOnlyList<(MonsterModel Monster, string? SlotName)> monsters = encounter.CreateMonsters();

        Assert.Equal("TrainingDummyIntro", encounter.Name);
        Assert.Single(monsters);
        Assert.IsType<TrainingDummy>(monsters[0].Monster);
    }

    [Fact]
    public void EmptyBatch_IsSafeForPluralCall_AndRejectedClearlyBySingularCompatibilityCall()
    {
        var encounter = new EncounterDefinition(
            () => Array.Empty<(MonsterModel Monster, string? SlotName)>());

        Assert.Empty(encounter.CreateMonsters());
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(encounter.CreateMonster);
        Assert.Contains("no monsters", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyConstructorSignatures_RemainAvailableForCompiledConsumers()
    {
        Type encounterType = typeof(EncounterDefinition);
        Type tagsType = typeof(IReadOnlyList<EncounterTag>);
        Type slottedBatchType = typeof(IReadOnlyList<(MonsterModel Monster, string? SlotName)>);

        Assert.NotNull(encounterType.GetConstructor(
            [typeof(Func<MonsterModel>), tagsType, typeof(bool)]));
        Assert.NotNull(encounterType.GetConstructor(
            [typeof(Func<>).MakeGenericType(slottedBatchType), tagsType, typeof(bool)]));
        Assert.NotNull(encounterType.GetConstructor(
            [
                typeof(Func<,>).MakeGenericType(typeof(Rng), slottedBatchType),
                tagsType,
                typeof(bool),
            ]));
    }

    [Fact]
    public void LegacyNullTagsCalls_RemainSourceCompatible()
    {
        Func<MonsterModel> singleFactory = () => ModelDb.Monster<TrainingDummy>();
        Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>> batchFactory =
            () => [(ModelDb.Monster<TrainingDummy>(), null)];
        Func<Rng, IReadOnlyList<(MonsterModel Monster, string? SlotName)>> rngBatchFactory =
            _ => [(ModelDb.Monster<TrainingDummy>(), null)];

        var single = new EncounterDefinition(singleFactory, null);
        var batch = new EncounterDefinition(batchFactory, null);
        var rngBatch = new EncounterDefinition(rngBatchFactory, null);

        Assert.Multiple(
            () => Assert.Equal(EncounterDefinition.DefaultName, single.Name),
            () => Assert.Equal(EncounterDefinition.DefaultName, batch.Name),
            () => Assert.Equal(EncounterDefinition.DefaultName, rngBatch.Name));
        Assert.Multiple(
            () => Assert.Single(single.CreateMonsters()),
            () => Assert.Single(batch.CreateMonsters()),
            () => Assert.Single(rngBatch.CreateMonsters(new Rng(7UL))));
    }
}
