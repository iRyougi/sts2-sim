using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>11格挡,另获5层下回合格挡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.Glitterstream</c>）。</summary>
public sealed class Glitterstream : CardModel
{
    public override bool GainsBlock => true;

    private decimal _block = 11m;
    private decimal _nextTurnBlock = 5m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        decimal blockNextTurnAmount = Hook.ModifyBlock(
            CombatState!, Owner.Creature, _nextTurnBlock, ValueProp.Move, this, cardPlay, out _);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<BlockNextTurnPower>(CombatState!, Owner.Creature, blockNextTurnAmount, Owner.Creature, this);
    }

    protected override void OnUpgrade()
    {
        _block += 2m;
        _nextTurnBlock += 2m;
    }
}
