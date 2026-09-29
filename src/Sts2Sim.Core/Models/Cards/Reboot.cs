using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Reboot : CardModel, ICardChoiceBaseValueProvider
{
    private int _cards = 4;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: _cards);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel card in Owner.PlayerCombatState!.Hand.Cards.ToArray())
            CardPileCmd.Add(card, PileType.Draw);
        await CardPileCmd.Shuffle(CombatState!, Owner);
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() => _cards += 2;
}
