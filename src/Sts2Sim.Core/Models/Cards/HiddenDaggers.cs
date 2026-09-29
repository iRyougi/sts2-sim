using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class HiddenDaggers : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CardCmd.Discard(await CardSelectCmd.FromHandForDiscard(CombatState!, Owner, 2, this));
        foreach (CardModel shiv in await Shiv.CreateInHand(Owner, 2, CombatState!))
        {
            if (IsUpgraded) CardCmd.Upgrade(shiv);
        }
    }
}
