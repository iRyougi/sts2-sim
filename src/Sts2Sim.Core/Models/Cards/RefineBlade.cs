using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>熔炼8点伤害,1层下回合能量。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.RefineBlade</c>）。</summary>
public sealed class RefineBlade : CardModel
{
    private decimal _forge = 8m;
    private const decimal NextTurnEnergy = 1m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await ForgeCmd.Forge(_forge, Owner, this);
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, NextTurnEnergy, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _forge += 4m;
}
