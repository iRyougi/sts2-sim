using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class PlowPowerTests : IDisposable
{
    public PlowPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(PlowTestMonster),
            typeof(PlowPower),
            typeof(StrengthPower),
            typeof(TemporaryStrengthPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task DamagingHitAtHpThreshold_ClearsStrengthAndForcesOneStunnedMove()
    {
        var combatState = new CombatState(new FakeRunState());
        combatState.AddPlayerCreature(Creature.CreateStandaloneForTests(50, 50));
        var monster = (PlowTestMonster)ModelDb.Monster<PlowTestMonster>().MutableClone();
        Creature owner = combatState.AddMonster(monster, CombatSide.Enemy);
        var engine = new CombatEngine(combatState);
        await engine.StartCombatAsync();
        await PowerCmd.Apply<StrengthPower>(combatState, owner, 3m, owner, null);
        await PowerCmd.Apply<TemporaryStrengthPower>(combatState, owner, 2m, owner, null);
        await PowerCmd.Apply<PlowPower>(combatState, owner, 5m, owner, null);

        await Damage(combatState, owner, 10m);

        Assert.NotNull(owner.GetPower<PlowPower>());
        Assert.NotNull(owner.GetPower<StrengthPower>());
        Assert.NotNull(owner.GetPower<TemporaryStrengthPower>());
        Assert.Equal("IDLE", owner.Monster!.NextMove!.StateId);

        await Damage(combatState, owner, 5m);

        Assert.Equal(5, owner.CurrentHp);
        Assert.Null(owner.GetPower<PlowPower>());
        Assert.Null(owner.GetPower<StrengthPower>());
        Assert.Null(owner.GetPower<TemporaryStrengthPower>());
        Assert.Equal("STUNNED", owner.Monster.NextMove!.StateId);
        Assert.True(owner.Monster.NextMove.MustPerformOnceBeforeTransitioning);

        await engine.EndPlayerTurnAsync();

        Assert.Equal("IDLE", owner.Monster.NextMove!.StateId);
        Assert.Equal(5, owner.CurrentHp);
    }

    private static Task<IReadOnlyList<DamageResult>> Damage(
        ICombatState combatState,
        Creature owner,
        decimal amount) =>
        CreatureCmd.Damage(
            combatState,
            new[] { owner },
            amount,
            ValueProp.Unblockable,
            dealer: null,
            cardSource: null,
            cardPlay: null);

    private sealed class PlowTestMonster : MonsterModel
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

        public RunRngSet Rng { get; } = new("plow-power-tests");

        public AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public AbstractRoom? CurrentRoom => null;
    }
}
