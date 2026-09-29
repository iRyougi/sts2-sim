using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Regalite : RelicModel
{
    private bool _usedThisTurn;
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (creator is null || creator != Owner || _usedThisTurn) return Task.CompletedTask;
        _usedThisTurn = true;
        return CreatureCmd.GainBlock(Owner.Creature.CombatState!, Owner.Creature, 4m,
            ValueProp.Unpowered, null, null);
    }

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature)) _usedThisTurn = false;
        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _usedThisTurn = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_usedThisTurn);
    }
}
