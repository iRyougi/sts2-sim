using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>若手牌无攻击牌则抽2张。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Impatience</c>）。</summary>
public sealed class Impatience : CardModel
{
    private int _draw = 2;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        if (!Owner.PlayerCombatState!.Hand.Cards.Any(c => c.Type == CardType.Attack))
        {
            await CardPileCmd.Draw(CombatState!, _draw, Owner, fromHandDraw: false);
        }
    }

    protected override void OnUpgrade() => _draw += 1;
}
