using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>保留整手牌，1层下回合能量，1层下回合星愿。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Convergence</c>）。</summary>
public sealed class Convergence : CardModel
{
    private const decimal NextTurnEnergy = 1m;
    private decimal _nextTurnStars = 1m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<RetainHandPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, NextTurnEnergy, Owner.Creature, this);
        await PowerCmd.Apply<StarNextTurnPower>(CombatState!, Owner.Creature, _nextTurnStars, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _nextTurnStars += 1m;
}
