using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class TerritorialPowerTests : IDisposable
{
    public TerritorialPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(TerritorialTestMonster),
            typeof(TerritorialPower),
            typeof(StrengthPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task OwnerSideTurnEnd_GrantsStrengthEqualToTerritorialAmount()
    {
        var combatState = new CombatState(new FakeRunState());
        var monster = (TerritorialTestMonster)ModelDb.Monster<TerritorialTestMonster>().MutableClone();
        Creature owner = combatState.AddMonster(monster, CombatSide.Enemy);
        await PowerCmd.Apply<TerritorialPower>(combatState, owner, 3m, owner, null);

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Player, Array.Empty<Creature>());

        Assert.Null(owner.GetPower<StrengthPower>());

        await Hook.AfterSideTurnEnd(combatState, CombatSide.Enemy, new[] { owner });

        StrengthPower strength = Assert.IsType<StrengthPower>(owner.GetPower<StrengthPower>());
        Assert.Equal(3, strength.Amount);
    }

    private sealed class TerritorialTestMonster : MonsterModel
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

        public RunRngSet Rng { get; } = new("territorial-power-tests");

        public AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public AbstractRoom? CurrentRoom => null;
    }
}
