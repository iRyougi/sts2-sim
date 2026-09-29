using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>13格挡,1层"保留手牌"。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Equilibrium</c>）。</summary>
public sealed class Equilibrium : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 13m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<RetainHandPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _block += 3m;
}
