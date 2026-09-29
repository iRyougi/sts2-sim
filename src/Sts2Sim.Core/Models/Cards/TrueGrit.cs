using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class TrueGrit : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _block = 7m;
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Block: (double)_block);
    public override bool GainsBlock => true;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, _block,
            ValueProp.Move, this, cardPlay);
        if (IsUpgraded)
        {
            CardModel? chosen = (await CardSelectCmd.FromHand(
                CombatState!, Owner, Owner.PlayerCombatState!.Hand.Cards,
                1, 1, this)).FirstOrDefault();
            if (chosen is not null) await CardPileCmd.Exhaust(CombatState!, chosen);
            return;
        }
        CardModel? random = Owner.RunState.Rng.CombatCardSelection
            .NextItem(Owner.PlayerCombatState!.Hand.Cards);
        if (random is not null) await CardPileCmd.Exhaust(CombatState!, random);
    }

    protected override void OnUpgrade() => _block += 2m;
}
