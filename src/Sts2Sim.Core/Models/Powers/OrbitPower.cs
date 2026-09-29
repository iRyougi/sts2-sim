using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class OrbitPower : PowerModel
{
    private const int EnergyPerTrigger = 4;
    private int _energySpent;

    public int EnergySpent => _energySpent;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override Task AfterEnergySpent(CardModel card, int amount)
    {
        if (card.Owner.Creature != Owner)
        {
            return Task.CompletedTask;
        }

        _energySpent += amount;
        while (_energySpent >= EnergyPerTrigger)
        {
            Owner.Player!.PlayerCombatState!.GainEnergy(Amount);
            _energySpent -= EnergyPerTrigger;
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_energySpent);
    }
}
