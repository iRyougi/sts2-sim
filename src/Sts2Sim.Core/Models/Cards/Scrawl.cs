using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Exhaust,手牌补满。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Scrawl</c>）。
/// 升级后追加 Retain（不是移除 Exhaust）。</summary>
public sealed class Scrawl : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        int count = CardPile.MaxCardsInHand - Owner.PlayerCombatState!.Hand.Cards.Count;
        await CardPileCmd.Draw(CombatState!, count, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
