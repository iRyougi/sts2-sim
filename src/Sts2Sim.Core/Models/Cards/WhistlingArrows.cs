using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class WhistlingArrows : CardModel, ICardChoiceBaseValueProvider
{
    private decimal Block => IsUpgraded ? 4m : 3m;

    public override bool GainsBlock => true;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)Block);

    protected override Task OnPlay(CardPlay cardPlay) =>
        CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);

    public override Task AfterArrowShot(Player owner, ArrowSlot? arrow, CardModel? source)
    {
        if (ReferenceEquals(owner, Owner) && arrow is not null && Pile?.Type == PileType.Discard)
            CardPileCmd.Add(this, PileType.Hand);
        return Task.CompletedTask;
    }
}
