using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>HauntPower</c>：持有者每打出一张 <see cref="Soul"/>，用 CombatTargets 随机选一个可命中的敌人，
/// 造成层数的无来源、不可格挡、不受加成伤害。</summary>
public sealed class HauntPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card is not Soul || cardPlay.Card.Owner.Creature != Owner)
            return;

        IReadOnlyList<Creature> hittableEnemies = Owner.CombatState!.HittableEnemies;
        if (hittableEnemies.Count == 0)
            return;

        Creature target = Owner.Player!.RunState.Rng.CombatTargets.NextItem(hittableEnemies)!;
        await CreatureCmd.Damage(Owner.CombatState, [target], Amount,
            ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
    }
}
