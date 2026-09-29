using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RippleBasin : RelicModel
{
    // 偏离 #121：用遗物私有逐回合标记替代未移植的 CombatManager.History 查询。
    private bool _attackPlayedThisTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _attackPlayedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner &&
            cardPlay.Card.Type == CardType.Attack)
        {
            _attackPlayedThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override Task BeforeSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner.Creature) ||
            _attackPlayedThisTurn)
        {
            return Task.CompletedTask;
        }

        return CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            4m,
            ValueProp.Unpowered,
            null,
            null);
    }

    public override Task AfterCombatEnd()
    {
        _attackPlayedThisTurn = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_attackPlayedThisTurn);
    }
}
