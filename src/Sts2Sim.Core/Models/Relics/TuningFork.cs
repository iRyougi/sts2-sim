using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class TuningFork : RelicModel
{
    private int _skillsPlayed;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Skill)
        {
            return;
        }

        _skillsPlayed++;
        if (_skillsPlayed < 10)
        {
            return;
        }

        _skillsPlayed %= 10;
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
