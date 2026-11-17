using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>己方箭矢被拔出或回收时获得 4 点 Unpowered 格挡（原版 <c>Twine</c>）。</summary>
public sealed class Twine : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterArrowDislodged(ImpaledPower power, ArrowSlot arrow, CardModel? source)
    {
        if (arrow.Owner == Owner && Owner.Creature.CombatState is { } combatState)
        {
            Flash();
            await CreatureCmd.GainBlock(combatState, Owner.Creature, 4m, ValueProp.Unpowered, null, null);
        }
    }
}
