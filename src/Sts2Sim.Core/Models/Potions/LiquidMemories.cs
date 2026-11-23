using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class LiquidMemories : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        CardModel? selected = (await CardSelectCmd.FromCombatPile(target.CombatState!, target.Player!,
            target.Player!.PlayerCombatState!.DiscardPile, 1, 1, this, cancelable: false)).FirstOrDefault();
        if (selected is not null)
        {
            selected.SetToFreeThisTurn();     // 原版 SetToFreeThisTurn：本回合内或打出一次之前免费（打出后恢复原价）
            CardPileCmd.Add(selected, PileType.Hand);
        }

    }
}
