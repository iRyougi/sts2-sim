using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>衍生 Token 牌 Joy（原版 <c>MegaCrit.Sts2.Core.Models.Cards.Joy</c>）。</summary>
public sealed class Joy : CardModel, ICardChoiceBaseValueProvider
{
    private int _energy = 1;
    private const int Cards = 1;

    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards, Energy: _energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainEnergy(_energy, Owner);
        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() => _energy += 1;
}
