using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ChargeBattery : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 7m;
    private decimal _energy = 1m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block, Energy: (double)_energy);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, _energy, Owner.Creature, this);
    }
    protected override void OnUpgrade() => _block += 3m;
}
