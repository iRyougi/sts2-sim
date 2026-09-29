using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,抽2张选1放回堆顶，升级移除 Exhaust。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.ThinkingAhead</c>）。</summary>
public sealed class ThinkingAhead : CardModel
{
    private int _draw = 2;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardPileCmd.Draw(CombatState!, _draw, Owner, fromHandDraw: false);
        CardModel? selected = (await CardSelectCmd.FromHand(CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards, 1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Top);
        }
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}
