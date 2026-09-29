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
public sealed class SlipperyPowerTests : IDisposable
{
    public SlipperyPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(SlipperyTestMonster), typeof(SlipperyPower) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Damage_CapsOwnerHpLossAtOne_AndConsumesOneStackPerDamagingHit()
    {
        var combatState = new CombatState(new FakeRunState());
        var monster = (SlipperyTestMonster)ModelDb.Monster<SlipperyTestMonster>().MutableClone();
        Creature owner = combatState.AddMonster(monster, CombatSide.Enemy);
        SlipperyPower slippery = Assert.IsType<SlipperyPower>(
            await PowerCmd.Apply<SlipperyPower>(combatState, owner, 2m, owner, null));

        DamageResult first = Assert.Single(await CreatureCmd.Damage(
            combatState,
            new[] { owner },
            10m,
            ValueProp.Move,
            dealer: null,
            cardSource: null,
            cardPlay: null));

        Assert.Equal(1, first.UnblockedDamage);
        Assert.Equal(19, owner.CurrentHp);
        Assert.Equal(1, slippery.Amount);

        DamageResult second = Assert.Single(await CreatureCmd.Damage(
            combatState,
            new[] { owner },
            10m,
            ValueProp.Move,
            dealer: null,
            cardSource: null,
            cardPlay: null));

        Assert.Equal(1, second.UnblockedDamage);
        Assert.Equal(18, owner.CurrentHp);
        Assert.Null(owner.GetPower<SlipperyPower>());
    }

    private sealed class SlipperyTestMonster : MonsterModel
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

        public RunRngSet Rng { get; } = new("slippery-power-tests");

        public AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public AbstractRoom? CurrentRoom => null;
    }
}
