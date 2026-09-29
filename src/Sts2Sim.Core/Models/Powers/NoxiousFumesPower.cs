using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class NoxiousFumesPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (!participants.Contains(Owner)) return;
        foreach (Creature enemy in Owner.CombatState!.HittableEnemies.ToArray())
            await PowerCmd.Apply<PoisonPower>(Owner.CombatState, enemy, Amount, Owner, null);
    }
}
