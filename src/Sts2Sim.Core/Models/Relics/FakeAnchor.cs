using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeAnchor : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override int MerchantCost => 50;

    public override Task BeforeCombatStart() =>
        CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            4m,
            ValueProp.Unpowered,
            null,
            null);
}
