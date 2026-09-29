using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>CountdownPower</c>：持有者所在一方回合开始时，用 <c>CombatTargets</c> 抽一名可命中敌人，
/// 施加层数的灾厄（施加者是持有者，无卡牌来源）。</summary>
public sealed class CountdownPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner))
            return;

        Creature? creature = Owner.Player!.RunState.Rng.CombatTargets.NextItem(Owner.CombatState!.HittableEnemies);
        if (creature is not null)
            await PowerCmd.Apply<DoomPower>(Owner.CombatState!, creature, Amount, Owner, null);
    }
}
