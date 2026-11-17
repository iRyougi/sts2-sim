using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PollinousCore : RelicModel
{
    private const int TurnsThreshold = 4; // DynamicVar "Turns"

    private int _handsSeen;

    // PollinousCore.UpdateDisplay (IsActivating is a 1s animation flag; always false at a decision point).
    private void UpdateStatus() =>
        Status = _handsSeen == TurnsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;

    public override RelicRarity Rarity => RelicRarity.Event;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad() => UpdateStatus();
    public override Task BeforeHandDraw(Player player)
    {
        if (player == Owner)
        {
            _handsSeen++;
            UpdateStatus();
        }

        return Task.CompletedTask;
    }
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner || _handsSeen < 4) return count;
        return count + 2m;
    }
    public override Task AfterModifyingHandDraw()
    {
        _handsSeen = 0;
        UpdateStatus();
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }
    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_handsSeen);
}
