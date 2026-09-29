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
        // TemporaryFreeThisCombat 必须一并计入，否则同一场战斗内再次使用本药水
        // 会重新选中它自己刚变免费的星耗/X 耗卡（EnergyCost 那条已由 CardModel 处理）。
        // 口径与 CardModel 内部的 hasTemporaryStarFree 一致。
        bool hasTemporaryFreeCost = card.TemporaryFreeThisTurn ||
            card.TemporaryFreeUntilPlayed ||
            card.TemporaryFreeThisCombat;
        bool hasTemporaryEnergyCost = hasTemporaryFreeCost || card.TemporaryCostOverrideThisTurn.HasValue;
        // Native checks both local and global cost. A global late hook (FreeSkillPower)
        // can display 0 while the card's own cost is still positive.
        return (!card.CostsXEnergy && (card.LocalEnergyCost > 0 || card.EnergyCost > 0)) ||
            (card.CostsXEnergy && !hasTemporaryEnergyCost) ||
            (card.HasStarCost && !hasTemporaryFreeCost);
    }
}
