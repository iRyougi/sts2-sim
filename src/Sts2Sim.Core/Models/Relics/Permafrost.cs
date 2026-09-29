using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Permafrost : RelicModel
{
    private bool _triggeredThisCombat;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        if (room is CombatRoom)
        {
            _triggeredThisCombat = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (_triggeredThisCombat ||
            cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Power)
        {
            return Task.CompletedTask;
        }

        _triggeredThisCombat = true;
        return CreatureCmd.GainBlock(
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
        builder.Append(_triggeredThisCombat);
    }
}
