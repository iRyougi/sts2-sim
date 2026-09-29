using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Turbo : CardModel, ICardChoiceBaseValueProvider
{
    private int _energy = 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: _energy);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(_energy, Owner);
        var voidCard = (CardModel)ModelDb.Card<Void>().MutableClone();
        voidCard.AssignOwner(Owner);
        await CardPileCmd.Generate(CombatState!, voidCard, PileType.Discard, Owner);
    }

    protected override void OnUpgrade() => _energy += 1;
}
