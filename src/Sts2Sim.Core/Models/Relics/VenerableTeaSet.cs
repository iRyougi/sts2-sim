using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Models.Relics;

public sealed class VenerableTeaSet : RelicModel
{
    private bool _isArmed;

    public override RelicRarity Rarity => RelicRarity.Common;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad() =>
        Status = _isArmed ? RelicStatus.Active : RelicStatus.Normal;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is RestSiteRoom)
        {
            _isArmed = true;
            Status = RelicStatus.Active;
        }

        return Task.CompletedTask;
    }

    public override Task AfterEnergyReset(Player player)
    {
        if (_isArmed && ReferenceEquals(player, Owner))
        {
            _isArmed = false;
            Status = RelicStatus.Normal;
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
