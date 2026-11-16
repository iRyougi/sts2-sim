namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Rocket : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 209, 199);
    public override int MaxInitialHp => MinInitialHp;
    private int CalculatingDamage => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);
    private int PreciseSmashDamage => Ascension(AscensionLevel.DeadlyEnemies, 20, 18);
    private int ChargeStrength => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);
    private int RocketClawDamage => Ascension(AscensionLevel.DeadlyEnemies, 35, 31);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        foreach (Creature player in Creature.CombatState!.Allies)
            await PowerCmd.Apply<SurroundedPower>(Creature.CombatState!, player, 1m, Creature, null);
        await PowerCmd.Apply<BackAttackRightPower>(Creature.CombatState!, Creature, 1m, Creature, null);
        await PowerCmd.Apply<CrabRagePower>(Creature.CombatState!, Creature, 1m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var calculating = new MoveState("CALCULATING_MOVE", _ => DamageCmd.Attack(CalculatingDamage).FromMonster(this).Execute(), new SingleAttackIntent(CalculatingDamage));
        var preciseSmash = new MoveState("PRECISE_SMASH_MOVE", PreciseSmashMove, new SingleAttackIntent(PreciseSmashDamage));
        // v0.113.1 registers a second PRECISE_SMASH state that no follow-up reaches in the normal cycle.
        var preciseSmash2 = new MoveState("PRECISE_SMASH_MOVE_2", PreciseSmashMove, new SingleAttackIntent(PreciseSmashDamage));
        var charge = new MoveState("CHARGE_UP_MOVE", _ => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, ChargeStrength, Creature, null), new BuffIntent());
        var rocketClaw = new MoveState("ROCKET_CLAW_MOVE", _ => DamageCmd.Attack(RocketClawDamage).FromMonster(this).Execute(), new SingleAttackIntent(RocketClawDamage));
        var recharge = new MoveState("RECHARGE_MOVE", _ => Task.CompletedTask, new SleepIntent());
        calculating.FollowUpState = preciseSmash;
        preciseSmash.FollowUpState = charge;
        charge.FollowUpState = rocketClaw;
        rocketClaw.FollowUpState = recharge;
        recharge.FollowUpState = calculating;
        return new MonsterMoveStateMachine(
            [calculating, preciseSmash, preciseSmash2, charge, rocketClaw, recharge],
            calculating);
    }

    private Task PreciseSmashMove(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(PreciseSmashDamage).FromMonster(this).Execute();

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
