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
public sealed class IllusionPowerTests : IDisposable
{
    public IllusionPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(IllusionTestMonster),
            typeof(IllusionPower),
            typeof(MinionPower),
            typeof(StrengthPower),
            typeof(VulnerablePower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Death_PreservesBuffsAndPerformsOneReviveMoveBeforeReturningToPriorState()
    {
        (CombatEngine engine, Creature owner) = await StartCombat();
        IllusionPower illusion = Assert.IsType<IllusionPower>(
            await PowerCmd.Apply<IllusionPower>(engine.State, owner, 1m, owner, null));
        await PowerCmd.Apply<StrengthPower>(engine.State, owner, 2m, owner, null);
        await PowerCmd.Apply<VulnerablePower>(engine.State, owner, 3m, owner, null);

        Assert.NotNull(owner.GetPower<MinionPower>());

        await DealLethalDamage(engine.State, owner);

        Assert.True(owner.IsDead);
        Assert.True(illusion.IsReviving);
        Assert.Equal("REVIVE_MOVE", owner.Monster!.NextMove!.StateId);
        Assert.NotNull(owner.GetPower<IllusionPower>());
        Assert.NotNull(owner.GetPower<MinionPower>());
        Assert.NotNull(owner.GetPower<StrengthPower>());
        Assert.Null(owner.GetPower<VulnerablePower>());
        Assert.False(engine.CheckWinCondition());

        await engine.EndPlayerTurnAsync();

        Assert.True(owner.IsAlive);
        Assert.Equal(owner.MaxHp, owner.CurrentHp);
        Assert.False(illusion.IsReviving);
        Assert.Equal("IDLE", owner.Monster.NextMove!.StateId);
        Assert.True(engine.IsInProgress);
    }

    [Fact]
    public async Task RevivingOwner_IsNotDamagedThroughCreatureCommand()
    {
        (CombatEngine engine, Creature owner) = await StartCombat();
        IllusionPower illusion = Assert.IsType<IllusionPower>(
            await PowerCmd.Apply<IllusionPower>(engine.State, owner, 1m, owner, null));
        await DealLethalDamage(engine.State, owner);
        await CreatureCmd.Heal(owner, 1m);

        DamageResult result = Assert.Single(await CreatureCmd.Damage(
            engine.State,
            new[] { owner },
            5m,
            ValueProp.Unblockable,
            dealer: null,
            cardSource: null,
            cardPlay: null));

        Assert.True(illusion.IsReviving);
        Assert.Equal(0, result.UnblockedDamage);
        Assert.Equal(1, owner.CurrentHp);
    }

    [Fact]
    public async Task RevivingOwner_RejectsNewBuffsAndDebuffsUntilReviveCompletes()
    {
        (CombatEngine engine, Creature owner) = await StartCombat();
        await PowerCmd.Apply<IllusionPower>(engine.State, owner, 1m, owner, null);
        await DealLethalDamage(engine.State, owner);

        StrengthPower? blockedBuff = await PowerCmd.Apply<StrengthPower>(engine.State, owner, 2m, owner, null);
        VulnerablePower? blockedDebuff = await PowerCmd.Apply<VulnerablePower>(engine.State, owner, 3m, owner, null);

        Assert.Null(blockedBuff);
        Assert.Null(blockedDebuff);
        Assert.Null(owner.GetPower<StrengthPower>());
        Assert.Null(owner.GetPower<VulnerablePower>());

        await engine.EndPlayerTurnAsync();

        Assert.NotNull(await PowerCmd.Apply<StrengthPower>(engine.State, owner, 2m, owner, null));
        Assert.NotNull(await PowerCmd.Apply<VulnerablePower>(engine.State, owner, 3m, owner, null));
    }

    private static async Task<(CombatEngine Engine, Creature Owner)> StartCombat()
    {
        var combatState = new CombatState(new FakeRunState());
        combatState.AddPlayerCreature(Creature.CreateStandaloneForTests(50, 50));
        var primary = (IllusionTestMonster)ModelDb.Monster<IllusionTestMonster>().MutableClone();
        combatState.AddMonster(primary, CombatSide.Enemy, "primary");
        var monster = (IllusionTestMonster)ModelDb.Monster<IllusionTestMonster>().MutableClone();
        Creature owner = combatState.AddMonster(monster, CombatSide.Enemy, "illusion");
        var engine = new CombatEngine(combatState);
        await engine.StartCombatAsync();
        return (engine, owner);
    }

    private static async Task DealLethalDamage(ICombatState combatState, Creature owner)
    {
        await CreatureCmd.Damage(
            combatState,
            new[] { owner },
            owner.CurrentHp,
            ValueProp.Unblockable,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }

    private sealed class IllusionTestMonster : MonsterModel
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

        public RunRngSet Rng { get; } = new("illusion-power-tests");

        public AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public AbstractRoom? CurrentRoom => null;
    }
}
