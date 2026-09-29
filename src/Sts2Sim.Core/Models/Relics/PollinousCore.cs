using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PollinousCore : RelicModel
{
    private int _handsSeen;
    public override RelicRarity Rarity => RelicRarity.Event;
    public override Task BeforeHandDraw(Player player) { if (player == Owner) _handsSeen++; return Task.CompletedTask; }
    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner || _handsSeen < 4) return count;
        return count + 2m;
    }
    public override Task AfterModifyingHandDraw()
    {
        _handsSeen = 0;
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd() => Task.CompletedTask;
    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_handsSeen);
}
