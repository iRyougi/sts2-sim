using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>第一回合抽牌前在手牌生成 1 张 Joy 与 1 张 Sadness（原版 <c>BizarreLamp</c>）。</summary>
public sealed class BizarreLamp : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task BeforeHandDraw(Player player)
    {
        if (player == Owner && Owner.PlayerCombatState?.TurnNumber <= 1 &&
            Owner.Creature.CombatState is { } combatState)
        {
            var joy = (Joy)ModelDb.Card<Joy>().MutableClone();
            joy.AssignOwner(Owner);
            var sadness = (Sadness)ModelDb.Card<Sadness>().MutableClone();
            sadness.AssignOwner(Owner);
            Flash();
            await CardPileCmd.Generate(combatState, joy, PileType.Hand, Owner);
            await CardPileCmd.Generate(combatState, sadness, PileType.Hand, Owner);
        }
    }
}
