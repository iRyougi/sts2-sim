using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>目标玩家手里生成 2 张 Soul，生成者记为使用者。</summary>
public sealed class PotOfGhouls : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await Soul.CreateInHand(target.Player!, 2, target.CombatState!, Owner);
    }
}
