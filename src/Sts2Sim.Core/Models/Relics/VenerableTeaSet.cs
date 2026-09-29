using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class VenerableTeaSet : RelicModel
{
    private bool _isArmed;

    public override RelicRarity Rarity => RelicRarity.Common;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is RestSiteRoom)
        {
            _isArmed = true;
        }

        return Task.CompletedTask;
    }

    public override Task AfterEnergyReset(Player player)
    {
        if (_isArmed && ReferenceEquals(player, Owner))
        {
            _isArmed = false;
            player.PlayerCombatState!.GainEnergy(2m);
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_isArmed);
    }
}
