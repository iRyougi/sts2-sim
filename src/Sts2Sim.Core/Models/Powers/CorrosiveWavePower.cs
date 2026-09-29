using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class CorrosiveWavePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner) return;
        foreach (Creature enemy in Owner.CombatState!.HittableEnemies.ToArray())
            await PowerCmd.Apply<PoisonPower>(Owner.CombatState, enemy, Amount, Owner, null);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}
