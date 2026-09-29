using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TheAbacus : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task AfterShuffle(Player shuffler)
    {
        if (shuffler != Owner || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        await CreatureCmd.GainBlock(
            combatState,
            Owner.Creature,
            6m,
            ValueProp.Unpowered,
            cardSource: null,
            cardPlay: null);
    }
}
