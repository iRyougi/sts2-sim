using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>消耗手牌中的全部 Sadness，每张赋予 StretchPower（原版 <c>Growth</c>）。</summary>
public sealed class Growth : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    private decimal ExtraStretch => IsUpgraded ? 6m : 4m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        List<Sadness> sadnessCards = Owner.PlayerCombatState!.Hand.Cards.OfType<Sadness>().ToList();
        int count = sadnessCards.Count;
        foreach (Sadness sadness in sadnessCards)
        {
            await CardPileCmd.Exhaust(CombatState!, sadness);
        }

        await PowerCmd.Apply<StretchPower>(CombatState!, Owner.Creature, ExtraStretch * count, Owner.Creature, this);
    }
}
