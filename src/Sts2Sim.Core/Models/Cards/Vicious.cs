using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Vicious : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _cards = 1m;
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Cards: (double)_cards);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<ViciousPower>(CombatState!, Owner.Creature, _cards,
            Owner.Creature, this);
    }

    protected override void OnUpgrade() => _cards += 1m;
}
