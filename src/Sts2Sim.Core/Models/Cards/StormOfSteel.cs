using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class StormOfSteel : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        CardModel[] cards = Owner.PlayerCombatState!.Hand.Cards.ToArray();
        await CardCmd.Discard(cards);
        foreach (CardModel shiv in await Shiv.CreateInHand(Owner, cards.Length, CombatState!))
        {
            if (IsUpgraded) CardCmd.Upgrade(shiv);
        }
    }
}
