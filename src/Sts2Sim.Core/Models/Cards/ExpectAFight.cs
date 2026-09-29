using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ExpectAFight : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    private decimal _baseBlock = 15m;
    private decimal _blockPerStrength = 5m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 3;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        decimal block = _baseBlock + _blockPerStrength * Math.Max(
            0, Owner.Creature.GetPower<StrengthPower>()?.Amount ?? 0);
        return CreatureCmd.GainBlock(CombatState!, Owner.Creature, block,
            ValueProp.Move, this, cardPlay);
    }

    protected override void OnUpgrade()
    {
        _baseBlock += 1m;
        _blockPerStrength += 3m;
    }
}
