namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;

public sealed class RavenousPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDeath(Creature target)
    {
        if (target == Owner || target.Side != Owner.Side || Owner.IsDead || Owner.Monster is not CorpseSlug slug)
        {
            return;
        }

        slug.IsRavenous = true;
        slug.StartRavenousStun();
        await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Amount, Owner, null);
    }
}
