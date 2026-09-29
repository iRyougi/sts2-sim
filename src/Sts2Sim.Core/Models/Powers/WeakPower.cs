using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// Weak. Powered attacks made by the owner deal 75 percent damage.
/// </summary>
public sealed class WeakPower : PowerModel
{
    private const decimal DamageMultiplier = 0.75m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || !props.IsPoweredAttack())
        {
            return 1m;
        }

        if (target?.Player?.Relics.OfType<PaperKrane>().FirstOrDefault() is PaperKrane paperKrane)
        {
            return paperKrane.ModifyWeakMultiplier(
                target, DamageMultiplier, props, dealer, cardSource);
        }

        return DamageMultiplier;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        }
    }
}
