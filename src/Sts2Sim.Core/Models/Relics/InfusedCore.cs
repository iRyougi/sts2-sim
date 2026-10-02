using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class InfusedCore : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature) && Owner.PlayerCombatState!.TurnNumber <= 1)
        {
            for (int i = 0; i < 3; i++)
                await OrbCmd.Channel<LightningOrb>(Owner.Creature.CombatState!, Owner);
        }
    }

    public override decimal ModifyOrbValue(OrbModel orb, decimal value) =>
        ReferenceEquals(orb.Owner, Owner) && orb is LightningOrb ? value + 1m : value;
}
