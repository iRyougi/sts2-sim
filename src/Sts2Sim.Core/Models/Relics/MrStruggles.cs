using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MrStruggles : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    // 原版挂在 AfterPlayerTurnStart（抽完手牌后、AfterSideTurnStart 之前），同一时机的遗物先后与随机数消耗顺序才和原版一样
    public override Task AfterPlayerTurnStart(Player player)
    {
        if (player != Owner)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.Damage(
            Owner.Creature.CombatState!,
            Owner.Creature.CombatState!.HittableEnemies,
            Owner.PlayerCombatState!.TurnNumber,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }
}
