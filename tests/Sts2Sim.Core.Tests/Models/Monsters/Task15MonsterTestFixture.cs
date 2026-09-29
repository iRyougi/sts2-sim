namespace Sts2Sim.Core.Tests.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

internal sealed class Task15MonsterTestFixture : IDisposable
{
    public Task15MonsterTestFixture(Type monsterType)
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            monsterType,
            typeof(Slimed),
            typeof(AscendersBane),
            typeof(RingingPower),
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(DivineRight),
            typeof(WeakPower),
            typeof(VulnerablePower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static async Task<(T Monster, CombatRoom Room, IReadOnlyList<Player> Players)> CreateCombatAsync<T>(
        int ascensionLevel,
        string seed,
        int playerCount = 2)
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

        var room = new CombatRoom(() => (T)ModelDb.Monster<T>().MutableClone());
        await room.Enter(runState);
        T monster = Assert.IsType<T>(Assert.Single(room.Engine.State.Enemies).Monster);
        return (monster, room, players.AsReadOnly());
    }

    public static async Task<IReadOnlyList<RingingPower>> ApplyRingingAsync(
        CombatRoom room,
        IReadOnlyList<Player> players)
    {
        var powers = new List<RingingPower>();
        foreach (Player player in players)
        {
            powers.Add(Assert.IsType<RingingPower>(
                await PowerCmd.Apply<RingingPower>(
                    room.Engine.State,
                    player.Creature,
                    1m,
                    room.Engine.State.Enemies.Single(),
                    cardSource: null)));
        }

        return powers.AsReadOnly();
    }

    public static async Task PerformAndRoll(MonsterModel monster, CombatRoom room)
    {
        await monster.PerformMove();
        monster.RollMove(room.Engine.State.Allies);
    }
}
