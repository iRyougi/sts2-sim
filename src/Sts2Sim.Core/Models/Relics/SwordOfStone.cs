using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SwordOfStone : RelicModel
{
    private int _eliteVictories;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterCombatVictory()
    {
        if (Owner.RunState.CurrentRoom is not CombatRoom { RoomType: RoomType.Elite })
        {
            return;
        }

        _eliteVictories++;
        if (_eliteVictories >= 5)
        {
            await RelicCmd.Replace(
                this,
                (SwordOfJade)ModelDb.Relic<SwordOfJade>().MutableClone());
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_eliteVictories);
    }
}
