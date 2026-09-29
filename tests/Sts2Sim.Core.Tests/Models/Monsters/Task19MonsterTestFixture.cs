namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal sealed class Task19MonsterTestFixture : IDisposable
{
    public Task19MonsterTestFixture()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(PhrogDeathPreventionPower),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static async Task<(PhrogParasite Monster, CombatRoom Room, IReadOnlyList<Player> Players)>
        CreateCombatAsync(
            int ascensionLevel,
            string seed,
            int playerCount = 1)
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel);
        var players = new List<Player>();
        for (int index = 0; index < playerCount; index++)
        {
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            players.Add(player);
        }

        var room = new CombatRoom(() => new (MonsterModel Monster, string? SlotName)[]
        {
            ((MonsterModel)ModelDb.Monster<PhrogParasite>().MutableClone(), "phrog"),
        });
        runState.PushRoom(room);
        await room.Enter(runState);

        PhrogParasite monster = Assert.IsType<PhrogParasite>(
            Assert.Single(room.Engine.State.Enemies).Monster);
        return (monster, room, players.AsReadOnly());
    }

    public static void ForceMove(PhrogParasite monster, string stateId)
    {
        MoveState state = Assert.IsType<MoveState>(monster.MoveStateMachine!.States[stateId]);
        monster.SetMoveImmediate(state, forceTransition: true);
    }
}

internal sealed class PhrogDeathPreventionPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override bool ShouldDieLate(Creature creature) => creature != Owner;

    public override Task AfterPreventingDeath(Creature creature) => CreatureCmd.Heal(creature, 1m);
}
