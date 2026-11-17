using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GalacticDust : RelicModel
{
    private const int StarsThreshold = 10; // StarsVar(10)

    private int _starsSpent;

    // GalacticDust.UpdateDisplay (IsActivating is a 1s animation flag; always false at a decision point).
    private void UpdateStatus() =>
        Status = _starsSpent == StarsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad() => UpdateStatus();

    public override async Task AfterStarsSpent(int amount, Player spender)
    {
        if (spender != Owner)
        {
            return;
        }

        _starsSpent += amount;
        UpdateStatus();
        int triggers = _starsSpent / 10;
        if (triggers == 0)
        {
            return;
        }

        _starsSpent %= 10;
        UpdateStatus();
        await CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            10m * triggers,
            ValueProp.Unpowered,
            null,
            null);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_starsSpent);
    }
}
