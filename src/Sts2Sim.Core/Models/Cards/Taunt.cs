using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Taunt : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 6m;
    private decimal _vulnerable = 1m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Block: (double)_block);
    public override bool GainsBlock => true;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block,
            ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, _vulnerable,
            Owner.Creature, this);
    }

    protected override void OnUpgrade() => UpgradeValues();

    private void UpgradeValues()
    {
        _block += 1m;
        _vulnerable += 1m;
    }
}
