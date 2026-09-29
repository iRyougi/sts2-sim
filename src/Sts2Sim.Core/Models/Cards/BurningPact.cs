using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class BurningPact : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: _cards);
    private int _cards = 2;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.FromHand(
            CombatState!, Owner, Owner.PlayerCombatState!.Hand.Cards, 1, 1, this)).FirstOrDefault();
        if (selected is not null) await CardPileCmd.Exhaust(CombatState!, selected);
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() => _cards++;
}
