namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TerrorEel : MonsterModel
{
    private MoveState _terrorState = null!;

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 150, 140);
    public override int MaxInitialHp => MinInitialHp;

    public MoveState TerrorState
    {
        get => _terrorState;
        private set
        {
            AssertMutable();
            _terrorState = value;
        }
    }

    private int ShriekAmount => Ascension(AscensionLevel.ToughEnemies, 75, 70);
    private int CrashDamage => Ascension(AscensionLevel.DeadlyEnemies, 18, 16);
    private int ThrashDamage => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<ShriekPower>(Creature.CombatState!, Creature, ShriekAmount, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var crash = new MoveState("CRASH_MOVE", Crash, new SingleAttackIntent(() => CrashDamage));
        var thrash = new MoveState(
            "THRASH_MOVE",
            Thrash,
            new MultiAttackIntent(() => ThrashDamage, () => 3),
            new BuffIntent());
        var stun = new MoveState("STUN_MOVE", Stun, new StunIntent());
        TerrorState = new MoveState("TERROR_MOVE", Terror, new DebuffIntent());
        crash.FollowUpState = thrash;
        thrash.FollowUpState = crash;
        stun.FollowUpState = TerrorState;
        TerrorState.FollowUpState = crash;
        return new MonsterMoveStateMachine([crash, thrash, stun, TerrorState], crash);
    }

    private Task Crash(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(CrashDamage).FromMonster(this).Execute();

    private async Task Thrash(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ThrashDamage).WithHitCount(3).FromMonster(this).Execute();
        await PowerCmd.Apply<VigorPower>(Creature.CombatState!, Creature, 6m, Creature, null);
    }

    private Task Stun(IReadOnlyList<Creature> targets) => Task.CompletedTask;

    private async Task Terror(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 99m, Creature, null);
        }
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
