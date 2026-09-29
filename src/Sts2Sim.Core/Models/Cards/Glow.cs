using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>+1星愿,抽1张,获1层下回合抽牌。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Glow</c>）。</summary>
public sealed class Glow : CardModel
{
    private decimal _stars = 1m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainStars(_stars, Owner);
        await CardPileCmd.Draw(CombatState!, 1, Owner, fromHandDraw: false);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _stars += 1m;
}
