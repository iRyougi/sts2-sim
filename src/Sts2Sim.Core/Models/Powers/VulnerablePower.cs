using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>
/// 易伤。基础倍率 1.5；按原版顺序应用攻击者的 PaperPhrog、CrueltyPower，最后是受击者的 DebilitatePower。
/// </summary>
public sealed class VulnerablePower : PowerModel
{
    private const decimal DamageMultiplier = 1.5m;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner)
        {
            return 1m;
        }

        if (!props.IsPoweredAttack())
        {
            return 1m;
        }

        decimal multiplier = DamageMultiplier;
        if (dealer is not null)
        {
            PaperPhrog? paperPhrog = dealer.Player?.Relics.OfType<PaperPhrog>().FirstOrDefault();
            if (paperPhrog is not null)
                multiplier = paperPhrog.ModifyVulnerableMultiplier(target, multiplier, props, dealer, cardSource);

            CrueltyPower? crueltyPower = dealer.GetPower<CrueltyPower>()
                ?? dealer.PetOwner?.Creature.GetPower<CrueltyPower>();
            if (crueltyPower is not null)
                multiplier = crueltyPower.ModifyVulnerableMultiplier(target, multiplier, props, dealer, cardSource);
        }

        if (target.GetPower<DebilitatePower>() is { } debilitate)
            multiplier = debilitate.ModifyVulnerableMultiplier(target, multiplier, props, dealer, cardSource);

        return multiplier;
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Enemy)
        {
            await PowerCmd.TickDownDuration(Owner.CombatState!, this);
        }
    }
}
