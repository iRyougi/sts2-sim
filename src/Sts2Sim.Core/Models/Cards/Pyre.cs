using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Pyre : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _energy = 1m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Energy: (double)_energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<PyrePower>(CombatState!, Owner.Creature, _energy, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _energy += 1m;
}
