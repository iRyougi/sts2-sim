using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class AutomationPower : PowerModel
{
    private const int CardsPerTrigger = 10;
    private int _cardsLeft = CardsPerTrigger;

    public int CardsLeft => _cardsLeft;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (card.Owner != Owner.Player)
        {
            return Task.CompletedTask;
        }

        _cardsLeft--;
        if (_cardsLeft <= 0)
        {
            Owner.Player!.PlayerCombatState!.GainEnergy(Amount);
            _cardsLeft = CardsPerTrigger;
        }
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_cardsLeft);
    }
}
