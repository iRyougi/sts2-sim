using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Retain+Exhaust,耗尽最多3张手牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Purity</c>）</summary>
public sealed class Purity : CardModel
{
    private int _count = 3;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => new[] { CardKeyword.Retain, CardKeyword.Exhaust };

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        IReadOnlyList<CardModel> selected = await CardSelectCmd.FromHand(
            CombatState!, Owner, Owner.PlayerCombatState!.Hand.Cards.Where(c => c != this),
            0, _count, this, cancelable: false);
        foreach (CardModel card in selected)
        {
            await CardPileCmd.Exhaust(cardPlay.Player.Creature.CombatState!, card);
        }
    }

    protected override void OnUpgrade() => _count += 2;
}
