using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>+1星愿,获3层下回合星愿。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.HiddenCache</c>）。</summary>
public sealed class HiddenCache : CardModel
{
    private decimal _stars = 1m;
    private decimal _nextTurnStars = 3m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PlayerCmd.GainStars(_stars, Owner);
        await PowerCmd.Apply<StarNextTurnPower>(CombatState!, Owner.Creature, _nextTurnStars, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _nextTurnStars += 1m;
}
