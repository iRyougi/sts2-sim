using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class TouchOfInsanity : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        CardModel? selected = (await CardSelectCmd.FromHand(target.CombatState!, target.Player!,
                target.Player!.PlayerCombatState!.Hand.Cards.Where(CostsEnergyOrStars),
                1, 1, this, cancelable: false)).FirstOrDefault();
        // 偏离 #315（2026-09-08）：权威 TouchOfInsanity.OnUse 结尾是 SetToFreeThisCombat()——
        // 整场战斗免费；此前误用 MakeFreeUntilPlayed()（打出一次即清除）。
        selected?.MakeFreeThisCombat();
    }

    private static bool CostsEnergyOrStars(CardModel card)
    {
        // Native checks both local and all-modifier costs, excluding X from the latter.
        // Another fixed positive cost still makes a mixed X card eligible.
        return card.LocalEnergyCost > 0 || card.LocalStarCost > 0 ||
            (!card.CostsXEnergy && card.EnergyCost > 0) ||
            (!card.CostsXStar && card.StarCost > 0);
    }
}
