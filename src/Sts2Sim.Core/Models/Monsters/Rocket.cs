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
    private int ReticleDamage => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);
    private int BeamDamage => Ascension(AscensionLevel.DeadlyEnemies, 20, 18);
    private int ChargeStrength => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);
    private int LaserDamage => Ascension(AscensionLevel.DeadlyEnemies, 35, 31);

    public override async Task AfterAddedToRoom()
    {
        await PowerCmd.Apply<BackAttackRightPower>(Creature.CombatState!, Creature, 1m, Creature, null);
        await PowerCmd.Apply<CrabRagePower>(Creature.CombatState!, Creature, 1m, Creature, null);
        foreach (Creature player in Creature.CombatState!.Allies)
            await PowerCmd.Apply<SurroundedPower>(Creature.CombatState!, player, 1m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var reticle = new MoveState("TARGETING_RETICLE_MOVE", _ => DamageCmd.Attack(ReticleDamage).FromMonster(this).Execute(), new SingleAttackIntent(ReticleDamage));
        var beam = new MoveState("PRECISION_BEAM_MOVE", _ => DamageCmd.Attack(BeamDamage).FromMonster(this).Execute(), new SingleAttackIntent(BeamDamage));
        var charge = new MoveState("CHARGE_UP_MOVE", _ => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, ChargeStrength, Creature, null), new BuffIntent());
        var laser = new MoveState("LASER_MOVE", _ => DamageCmd.Attack(LaserDamage).FromMonster(this).Execute(), new SingleAttackIntent(LaserDamage));
        var recharge = new MoveState("RECHARGE_MOVE", _ => Task.CompletedTask, new SleepIntent());
        reticle.FollowUpState = beam;
        beam.FollowUpState = charge;
        charge.FollowUpState = laser;
        laser.FollowUpState = recharge;
        recharge.FollowUpState = reticle;
        return new MonsterMoveStateMachine([reticle, beam, charge, laser, recharge], reticle);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
