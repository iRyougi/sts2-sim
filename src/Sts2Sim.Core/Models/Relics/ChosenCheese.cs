using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ChosenCheese : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task AfterCombatEnd() =>
        CreatureCmd.GainMaxHp(Owner.Creature, 1m);
}
