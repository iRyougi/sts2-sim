using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>消耗手中全部 Sadness，每消耗 1 张抽 1 张；消耗，升级去掉消耗（v0.114.0 新增，原版 <c>Optimism</c>）。</summary>
public sealed class Optimism : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<CardModel> sadnessCards = Owner.PlayerCombatState!.Hand.Cards.Where(c => c is Sadness).ToList();
        int cardCount = sadnessCards.Count;
        foreach (CardModel sadness in sadnessCards)
        {
            await CardPileCmd.Exhaust(CombatState!, sadness);
        }

        if (cardCount > 0)
        {
            await CardPileCmd.Draw(CombatState!, cardCount, Owner, fromHandDraw: false);
        }
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
