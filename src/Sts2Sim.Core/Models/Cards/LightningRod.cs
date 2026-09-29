using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class LightningRod : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 4m;
    private readonly decimal _rodAmount = 2m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public override bool GainsBlock => true;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);

    protected override async Task OnPlay(CardPlay play)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, play);
        await PowerCmd.Apply<LightningRodPower>(CombatState!, Owner.Creature,
            _rodAmount, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _block += 3m;
}
