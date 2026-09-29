using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class SecondWind : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 5m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Block: (double)_block);
    public override bool GainsBlock => true;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel card in Owner.PlayerCombatState!.Hand.Cards
                     .Where(card => card.Type != CardType.Attack).ToArray())
        {
            await CardPileCmd.Exhaust(CombatState!, card);
            await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block,
                ValueProp.Move, this, cardPlay);
        }
    }

    protected override void OnUpgrade() => _block += 2m;
}
