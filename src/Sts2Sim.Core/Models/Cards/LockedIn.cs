using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>从抽牌堆选择 1 张 Shoot 牌入手牌（原版 <c>LockedIn</c>）。</summary>
public sealed class LockedIn : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? picked = (await CardSelectCmd.FromCombatPile(
            CombatState!,
            Owner,
            Owner.PlayerCombatState!.DrawPile,
            minCount: 1,
            maxCount: 1,
            this,
            c => c.Tags.Contains(CardTag.Shoot))).FirstOrDefault();
        if (picked is not null)
        {
            CardPileCmd.Add(picked, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
