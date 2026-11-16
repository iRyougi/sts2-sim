using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>v0.113.1 Shrine of the Warlord card (#927).</summary>
public sealed class MightyShield : CardModel, ICardChoiceBaseValueProvider
{
    private const int DazedCount = 3;
    private decimal _block = 20m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: (double)_block, Cards: DazedCount);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block, ValueProp.Move, this, cardPlay);
        for (int index = 0; index < DazedCount; index++)
        {
            var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
            dazed.AssignOwner(Owner);
            await CardPileCmd.Generate(CombatState!, dazed, PileType.Draw, Owner, CardPilePosition.Random);
        }
    }

    protected override void OnUpgrade() => _block += 5m;
}
