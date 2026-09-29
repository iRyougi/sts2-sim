using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Anchor : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task BeforeCombatStart() =>
        CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            10m,
            ValueProp.Unpowered,
            null,
            null);
}
