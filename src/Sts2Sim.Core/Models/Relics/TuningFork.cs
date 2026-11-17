using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TuningFork : RelicModel
{
    private const int SkillsThreshold = 10; // CardsVar(10)

    private int _skillsPlayed;

    // TuningFork.UpdateDisplay (IsActivating is a 1s animation flag; always false at a decision point).
    private void UpdateStatus() =>
        Status = _skillsPlayed == SkillsThreshold - 1 ? RelicStatus.Active : RelicStatus.Normal;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    /// <summary>Source [SavedProperty] setter recompute on load (field restore bypasses the setter).</summary>
    internal override void RefreshStatusAfterLoad() => UpdateStatus();

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        _skillsPlayed++;
        UpdateStatus();
        if (_skillsPlayed < 10)
        {
            return;
        }

        _skillsPlayed %= 10;
        UpdateStatus();
        await CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            7m,
            ValueProp.Unpowered,
            null,
            cardPlay);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_skillsPlayed);
    }
}
