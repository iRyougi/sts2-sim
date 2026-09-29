namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal sealed class Task16MonsterTestFixture : IDisposable
{
    public Task16MonsterTestFixture()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static async Task<(T Monster, CombatRoom Room, IReadOnlyList<Player> Players)> CreateCombatAsync<T>(
        int ascensionLevel,
        string seed,
        int playerCount = 1,
        string? slotName = null)
        where T : MonsterModel
    {
        var runState = new RunState(seed, new Overgrowth(), ascensionLevel);
        var players = new List<Player>();
        for (int index = 0; index < playerCount; index++)
        {
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            players.Add(player);
        }

        var room = new CombatRoom(() => new[]
        {
            ((MonsterModel)ModelDb.Monster<T>().MutableClone(), slotName),
        });
        runState.PushRoom(room);
        await room.Enter(runState);
        T monster = Assert.IsType<T>(Assert.Single(room.Engine.State.Enemies).Monster);
        return (monster, room, players.AsReadOnly());
    }

    public static async Task PerformAndRoll(MonsterModel monster, CombatRoom room)
    {
        await monster.PerformMove();
        monster.RollMove(room.Engine.State.Allies);
    }
}
