namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal sealed class Task17MonsterTestFixture : IDisposable
{
    public Task17MonsterTestFixture()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static async Task<(IReadOnlyList<T> Monsters, CombatRoom Room, Player Player)>
        CreateCombatAsync<T>(
            int ascensionLevel,
            string seed,
            params (Action<T>? Configure, string? SlotName)[] entries)
        where T : MonsterModel
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => entries
            .Select(entry =>
            {
                var monster = (T)ModelDb.Monster<T>().MutableClone();
                entry.Configure?.Invoke(monster);
                return ((MonsterModel)monster, entry.SlotName);
            })
            .ToArray());
        runState.PushRoom(room);

        await room.Enter(runState);

        IReadOnlyList<T> monsters = room.Engine.State.Enemies
            .Select(creature => Assert.IsType<T>(creature.Monster))
            .ToArray();
        return (monsters, room, player);
    }

    public static T CreateStructuralMonster<T>(
        int ascensionLevel,
        string seed,
        Action<T>? configure = null,
        string? slotName = null)
        where T : MonsterModel
    {
        var combatState = new CombatState(new RunState(seed, new Overgrowth(), ascensionLevel));
        combatState.AddPlayerCreature(Creature.CreateStandaloneForTests(100, 100));
        var monster = (T)ModelDb.Monster<T>().MutableClone();
        configure?.Invoke(monster);
        combatState.AddMonster(monster, CombatSide.Enemy, slotName);
        monster.SetUpForCombat();
        monster.RollMove(combatState.Allies);
        return monster;
    }

    public static void ForceMove(MonsterModel monster, string stateId)
    {
        var state = Assert.IsType<Sts2Sim.Core.MonsterMoves.MoveState>(
            monster.MoveStateMachine!.States[stateId]);
        monster.SetMoveImmediate(state, forceTransition: true);
    }
}
