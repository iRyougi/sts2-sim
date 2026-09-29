using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public class JungleMazeAdventureTests : IDisposable
{
    public JungleMazeAdventureTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight), typeof(JungleMazeAdventure) });
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public async Task IsAllowed_AllowsLowHpSoloRunsButRejectsMultiplayerRunsWithAnyPlayerAtEighteenHp()
    {
        var runState = new RunState("jungle-is-allowed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        JungleMazeAdventure maze = ModelDb.Event<JungleMazeAdventure>();
        await CreatureCmd.LoseHp(runState, player.Creature, player.Creature.CurrentHp - 18m, ValueProp.Unblockable);

        Assert.True(maze.IsAllowed(runState));

        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(other);

        Assert.False(maze.IsAllowed(runState));
    }

    [Fact]
    public async Task IsAllowed_AllowsMultiplayerRunsWhenEveryPlayerHasAtLeastNineteenHp()
    {
        var runState = new RunState("jungle-is-allowed-nineteen-hp", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.AddPlayer(other);
        JungleMazeAdventure maze = ModelDb.Event<JungleMazeAdventure>();

        await CreatureCmd.LoseHp(runState, player.Creature, player.Creature.CurrentHp - 19m, ValueProp.Unblockable);

        Assert.True(maze.IsAllowed(runState));

        await CreatureCmd.LoseHp(runState, player.Creature, 1m, ValueProp.Unblockable);

        Assert.False(maze.IsAllowed(runState));
    }
    private static (RunState runState, Player player, JungleMazeAdventure ev) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var ev = (JungleMazeAdventure)ModelDb.Event<JungleMazeAdventure>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    [Fact]
    public void BeginEvent_OffersSoloQuestAndJoinForces()
    {
        (_, _, JungleMazeAdventure ev) = Setup("jungle-maze-a");

        Assert.Equal(2, ev.CurrentOptions.Count);
        Assert.Contains(ev.CurrentOptions, o => o.Key == "SOLO_QUEST");
        Assert.Contains(ev.CurrentOptions, o => o.Key == "JOIN_FORCES");
    }

    [Fact]
    public async Task SoloQuest_DealsEighteenUnblockableDamage_AndGrantsGoldNearOneFifty()
    {
        (_, Player player, JungleMazeAdventure ev) = Setup("jungle-maze-b");
        decimal hpBefore = player.Creature.CurrentHp;
        int goldBefore = player.Gold;

        await ev.ChooseOption(ev.CurrentOptions.Single(o => o.Key == "SOLO_QUEST"));

        Assert.Equal(hpBefore - 18m, player.Creature.CurrentHp);
        Assert.InRange(player.Gold - goldBefore, 135, 165);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task JoinForces_DealsNoDamage_AndGrantsGoldNearFifty()
    {
        (_, Player player, JungleMazeAdventure ev) = Setup("jungle-maze-c");
        decimal hpBefore = player.Creature.CurrentHp;
        int goldBefore = player.Gold;

        await ev.ChooseOption(ev.CurrentOptions.Single(o => o.Key == "JOIN_FORCES"));

        Assert.Equal(hpBefore, player.Creature.CurrentHp);
        Assert.InRange(player.Gold - goldBefore, 35, 65);
        Assert.True(ev.IsFinished);
    }
}