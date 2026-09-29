namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SpinyToad : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 121, 116);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 124, 119);
    private int ExplosionDamage => Ascension(AscensionLevel.DeadlyEnemies, 25, 23);
    private int LashDamage => Ascension(AscensionLevel.DeadlyEnemies, 19, 17);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var spikes = new MoveState("PROTRUDING_SPIKES_MOVE", _ => PowerCmd.Apply<ThornsPower>(Creature.CombatState!, Creature, 5m, Creature, null), new BuffIntent());
        var explosion = new MoveState("SPIKE_EXPLOSION_MOVE", Explosion, new SingleAttackIntent(ExplosionDamage));
        var lash = new MoveState("TONGUE_LASH_MOVE", _ => DamageCmd.Attack(LashDamage).FromMonster(this).Execute(), new SingleAttackIntent(LashDamage));
        spikes.FollowUpState = explosion;
        explosion.FollowUpState = lash;
        lash.FollowUpState = spikes;
        return new MonsterMoveStateMachine([spikes, explosion, lash], spikes);
    }

    private async Task Explosion(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ExplosionDamage).FromMonster(this).Execute();
        if (Creature.GetPower<ThornsPower>() is { } thorns)
            await PowerCmd.ModifyAmount(Creature.CombatState!, thorns, -5m, Creature, null);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
