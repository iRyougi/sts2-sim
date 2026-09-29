using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Overclock : CardModel, ICardChoiceBaseValueProvider
{
    private int _cards = 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: _cards);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
        var burn = (CardModel)ModelDb.Card<Burn>().MutableClone();
        burn.AssignOwner(Owner);
        await CardPileCmd.Generate(CombatState!, burn, PileType.Discard, Owner);
    }

    protected override void OnUpgrade() => _cards += 1;
}
