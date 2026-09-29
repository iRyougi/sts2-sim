using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Colossus : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);
    private decimal _block = 4m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        await PowerCmd.Apply<ColossusPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
    }

    protected override void OnUpgrade() => _block += 3m;
}
