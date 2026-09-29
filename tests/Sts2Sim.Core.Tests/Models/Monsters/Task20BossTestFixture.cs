namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal sealed class Task20BossTestFixture : IDisposable
{
    public Task20BossTestFixture()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static async Task<(T Monster, CombatRoom Room, IReadOnlyList<Player> Players)>
        CreateSingleCombatAsync<T>(
            int ascensionLevel,
            string seed,
            int playerCount = 1)
        where T : MonsterModel
    {
        (CombatRoom room, IReadOnlyList<Player> players) = await CreateCombatAsync(
            ascensionLevel,
            seed,
            playerCount,
            () => new[]
            {
                ((MonsterModel)ModelDb.Monster<T>().MutableClone(), (string?)null),
            });
        T monster = Assert.IsType<T>(Assert.Single(room.Engine.State.Enemies).Monster);
        return (monster, room, players);
    }

    public static async Task<(CombatRoom Room, IReadOnlyList<Player> Players)> CreateCombatAsync(
        int ascensionLevel,
        string seed,
        int playerCount,
        Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>> monsterFactory)
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel);
        var players = new List<Player>();
        for (int index = 0; index < playerCount; index++)
        {
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            players.Add(player);
        }

        var room = new CombatRoom(monsterFactory);
        runState.PushRoom(room);
        await room.Enter(runState);
        return (room, players.AsReadOnly());
    }

    public static void ForceMove(MonsterModel monster, string stateId)
    {
        MoveState state = Assert.IsType<MoveState>(monster.MoveStateMachine!.States[stateId]);
        monster.SetMoveImmediate(state, forceTransition: true);
    }

    public static void RollAfterPerformed(MonsterModel monster)
    {
        monster.MoveStateMachine!.OnMovePerformed(monster.NextMove!);
        monster.RollMove(monster.Creature.CombatState!.Allies);
    }
}
