using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Armaments : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Block: 5);
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    public override bool GainsBlock => true;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, 5m, ValueProp.Move, this, cardPlay);
        if (IsUpgraded)
        {
            foreach (CardModel card in Owner.PlayerCombatState!.Hand.Cards.Where(card => card.IsUpgradable))
                CardCmd.Upgrade(card);
            return;
        }

        CardModel? selected = (await CardSelectCmd.FromHand(
            CombatState!, Owner,
            Owner.PlayerCombatState!.Hand.Cards.Where(card => card.IsUpgradable),
            1, 1, this)).FirstOrDefault();
        if (selected is not null) CardCmd.Upgrade(selected);
    }
}
