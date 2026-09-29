using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class InfestedPowerTests : IDisposable
{
    public InfestedPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(InfestedTestMonster),
            typeof(Wriggler),
            typeof(InfestedPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task BatchDamageSnapshotsTargets_WhenOwnerDeathAddsWrigglers()
    {
        var combatState = new CombatState(new FakeRunState());
        combatState.AddPlayerCreature(Creature.CreateStandaloneForTests(50, 50));
        var infestedMonster = (InfestedTestMonster)ModelDb.Monster<InfestedTestMonster>().MutableClone();
        Creature owner = combatState.AddMonster(infestedMonster, CombatSide.Enemy, "phrog");
        var engine = new CombatEngine(combatState);
        await engine.StartCombatAsync();
        await PowerCmd.Apply<InfestedPower>(combatState, owner, 4m, owner, null);

        Assert.True(Hook.ShouldStopCombatFromEnding(combatState));

        await CreatureCmd.Damage(
            combatState,
            combatState.Enemies,
            owner.CurrentHp,
            ValueProp.Unblockable,
            dealer: null,
            cardSource: null,
            cardPlay: null);

        Creature[] wrigglers = combatState.Enemies.Where(enemy => enemy.Monster is Wriggler).ToArray();
        Assert.Equal(new[] { "wriggler1", "wriggler2", "wriggler3", "wriggler4" }, wrigglers.Select(w => w.SlotName));
        Assert.All(wrigglers, wriggler =>
        {
            Assert.True(Assert.IsType<Wriggler>(wriggler.Monster).StartStunned);
            Assert.Equal("SPAWNED_MOVE", wriggler.Monster!.NextMove!.StateId);
        });
        Assert.Null(owner.GetPower<InfestedPower>());
        Assert.False(engine.CheckWinCondition());

        foreach (Creature wriggler in wrigglers)
        {
            await CreatureCmd.Damage(
                combatState,
                new[] { wriggler },
                wriggler.CurrentHp,
                ValueProp.Unblockable,
                dealer: null,
                cardSource: null,
                cardPlay: null);
        }

        Assert.True(engine.CheckWinCondition());
        Assert.True(engine.Won);
    }

    private sealed class InfestedTestMonster : MonsterModel
    {
        public override int MinInitialHp => 20;

        public override int MaxInitialHp => 20;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var idle = new MoveState("IDLE", _ => Task.CompletedTask, new BuffIntent());
            idle.FollowUpState = idle;
            return new MonsterMoveStateMachine(new[] { idle }, idle);
        }
    }

    private sealed class FakeRunState : IRunState
    {
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; } = new("infested-power-tests");

        public AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public AbstractRoom? CurrentRoom => null;
    }
}
