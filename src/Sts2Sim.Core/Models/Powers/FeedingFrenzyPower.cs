using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class FeedingFrenzyPower : TemporaryStrengthPower
{
    public override bool AllowNegative => false;

    // Preserve the upstream TemporaryStrengthPower contract locally: Strength owns the
    // damage contribution, while this power tracks how much to revert at turn end.
    public override async Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource)
    {
        await PowerCmd.Apply<StrengthPower>(target.CombatState!, target, amount, applier, cardSource);
    }

    public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this && amount != Amount)
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, amount, applier, cardSource);
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay) => 0m;

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        await PowerCmd.Remove(this);
        await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, -Amount, Owner, null);
    }
}
