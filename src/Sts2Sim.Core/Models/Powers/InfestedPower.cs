using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Releases four stunned Wrigglers when its owner dies.</summary>
public sealed class InfestedPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDeath(Creature target)
    {
        if (target != Owner)
        {
            return;
        }

        for (int index = 0; index < 4; index++)
        {
            var wriggler = (Wriggler)ModelDb.Monster<Wriggler>().MutableClone();
            wriggler.StartStunned = true;
            await CreatureCmd.Add(wriggler, Owner.CombatState!, Owner.Side, $"wriggler{index + 1}");
        }
    }

    public override bool ShouldStopCombatFromEnding() => true;
}