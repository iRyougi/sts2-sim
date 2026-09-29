using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>偏离 #192：省略真实源码的绿色烟雾和短暂等待；二者仅为 VFX 时序，所有目标的中毒结算保持不变。</summary>
public sealed class TwistedFunnel : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        // 本引擎在 SetupPlayerTurnAsync 之后才加一回合数；0 是首回合的等价时刻。
        if (!participants.Contains(Owner.Creature) || Owner.PlayerCombatState?.TurnNumber != 0)
        {
            return;
        }

        foreach (Creature enemy in Owner.Creature.CombatState!.HittableEnemies.ToArray())
        {
            await PowerCmd.Apply<PoisonPower>(
                Owner.Creature.CombatState, enemy, 4m, Owner.Creature, null);
        }
    }
}
