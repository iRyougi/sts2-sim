using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FestivePopper : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        // 偏离 #113：无独立 AfterPlayerTurnStart；用 AfterSideTurnStart 保留持有者首回合一次的效果。
        if (!participants.Contains(Owner.Creature) ||
            Owner.PlayerCombatState?.TurnNumber != 1)
        {
            return;
        }

        var combatState = Owner.Creature.CombatState!;
        await CreatureCmd.Damage(
            combatState,
            combatState.HittableEnemies.ToArray(),
            9m,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            null);
    }
}
