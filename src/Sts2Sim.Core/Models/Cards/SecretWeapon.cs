using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,抽牌堆选1张攻击牌进手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.SecretWeapon</c>），
/// 选择经过选牌决策源。升级后移除 Exhaust。</summary>
public sealed class SecretWeapon : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel? selected = (await CardSelectCmd.SelectCardsAsync(
            CombatState!, Owner, Owner.PlayerCombatState!.DrawPile.Cards.Where(c => c.Type == CardType.Attack),
            1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Hand);
        }

        await Task.CompletedTask;
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
