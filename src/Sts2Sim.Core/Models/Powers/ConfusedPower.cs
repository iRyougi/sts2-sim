using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class ConfusedPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (!ReferenceEquals(card.Owner, Owner.Player) ||
            card.CanonicalEnergyCostValue < 0)
        {
            return Task.CompletedTask;
        }

        int randomizedCost = Owner.Player!.RunState.Rng.CombatEnergyCosts.NextInt(4);
        if (!card.CostsXEnergy)
        {
            card.SetTemporaryCostOverrideThisCombat(randomizedCost);
        }
        return Task.CompletedTask;
    }
}
