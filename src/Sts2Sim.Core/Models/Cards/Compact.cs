using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Compact : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 6m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        CardModel[] statuses = Owner.PlayerCombatState!.Hand.Cards
            .Where(card => card.IsTransformable && card.Type == CardType.Status)
            .ToArray();
        var transformations = new List<CardTransformation>();
        foreach (CardModel status in statuses)
        {
            var fuel = (Fuel)ModelDb.Card<Fuel>().MutableClone();
            fuel.AssignOwner(Owner);
            if (IsUpgraded) CardCmd.Upgrade(fuel);
            transformations.Add(new CardTransformation(status, fuel));
        }
        await CardCmd.Transform(transformations, null);
    }
    protected override void OnUpgrade() => _block += 1m;
}
