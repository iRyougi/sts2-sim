using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class ShrugItOff : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 8m;
    private int _cards = 1;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Block: (double)_block, Cards: _cards);
    public override bool GainsBlock => true;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block,
            ValueProp.Move, this, cardPlay);
        await CardPileCmd.Draw(CombatState!, _cards, Owner, fromHandDraw: false);
    }

    protected override void OnUpgrade() => _block += 3m;
}
