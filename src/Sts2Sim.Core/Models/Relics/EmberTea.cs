using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class EmberTea : RelicModel
{
    private int _combatsLeft = 5;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool IsUsedUp => _combatsLeft <= 0;

    public int CombatsLeft => Math.Max(0, _combatsLeft);

    public override async Task AfterRoomEntered(AbstractRoom room)
    {
        if (IsUsedUp || room is not CombatRoom)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(
            Owner.Creature.CombatState!,
            Owner.Creature,
            2m,
            null,
            null);
        _combatsLeft--;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_combatsLeft);
    }
}
