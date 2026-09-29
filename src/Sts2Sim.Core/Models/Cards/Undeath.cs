using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Undeath</c>：0 费，获得 7（升级 9）格挡，然后把本牌的一张复制放入弃牌堆。</summary>
public sealed class Undeath : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;

    public override bool GainsBlock => true;

    private decimal Block => IsUpgraded ? 9m : 7m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        await CardPileCmd.Generate(CombatState!, CreateClone(), PileType.Discard, Owner);
    }
}
