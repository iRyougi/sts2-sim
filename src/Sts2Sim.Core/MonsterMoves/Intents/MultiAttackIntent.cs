namespace Sts2Sim.Core.MonsterMoves.Intents;

using Sts2Sim.Core.Entities.Creatures;

/// <summary>Fixed or dynamically calculated multi-attack intent data; rendering-only label fields are omitted.</summary>
public sealed class MultiAttackIntent : AttackIntent
{
    private readonly Func<int> _repeatCalc;

    public MultiAttackIntent(int damage, int repeat)
        : this(() => damage, () => repeat)
    {
    }

    public MultiAttackIntent(Func<decimal> damageCalc, Func<int> repeatCalc)
    {
        ArgumentNullException.ThrowIfNull(damageCalc);
        ArgumentNullException.ThrowIfNull(repeatCalc);
        DamageCalc = damageCalc;
        _repeatCalc = repeatCalc;
    }

    public override int Repeats => _repeatCalc();

    public override int GetTotalDamage(IEnumerable<Creature> targets, Creature owner) =>
        GetSingleDamage(targets, owner) * Repeats;
}
