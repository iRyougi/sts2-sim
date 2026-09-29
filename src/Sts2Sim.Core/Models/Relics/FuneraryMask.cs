using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>首回合抽牌前，逐张把 3 张 Soul 随机洗入抽牌堆。</summary>
public sealed class FuneraryMask : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner || Owner.PlayerCombatState!.TurnNumber != 1)
            return;

        foreach (Soul soul in Soul.Create(Owner, 3))
        {
            await CardPileCmd.Generate(Owner.Creature.CombatState!, soul, PileType.Draw, Owner,
                CardPilePosition.Random);
        }
    }
}
