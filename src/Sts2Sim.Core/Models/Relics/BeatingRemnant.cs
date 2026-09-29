using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class BeatingRemnant : RelicModel
{
    private const int MaxHpLossPerTurn = 20;
    private int _hpLostThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _hpLostThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOsty(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Owner.Creature.CombatState?.IsLiveCombat() != true)
        {
            return amount;
        }

        if (target != Owner.Creature)
        {
            return amount;
        }

        decimal remaining = Math.Max(MaxHpLossPerTurn - _hpLostThisTurn, 0);
        return Math.Min(amount, remaining);
    }

    public override Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Owner.Creature.CombatState?.IsLiveCombat() != true)
        {
            return Task.CompletedTask;
        }

        if (target == Owner.Creature)
        {
            _hpLostThisTurn += result.UnblockedDamage;
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_hpLostThisTurn);
    }
}
