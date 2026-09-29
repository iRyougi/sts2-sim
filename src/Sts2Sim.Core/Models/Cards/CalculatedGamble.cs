using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class CalculatedGamble : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel[] cards = Owner.PlayerCombatState!.Hand.Cards.ToArray();
        await CardCmd.DiscardAndDraw(cards, cards.Length);
    }
    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
