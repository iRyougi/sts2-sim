using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class PrimalForce : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        foreach (CardModel attack in Owner.PlayerCombatState!.Hand.Cards
                     .Where(card => card.IsTransformable && card.Type == CardType.Attack).ToArray())
        {
            var rock = (GiantRock)ModelDb.Card<GiantRock>().MutableClone();
            if (IsUpgraded) CardCmd.Upgrade(rock);
            await CardCmd.Transform(attack, rock);
        }
    }
}
