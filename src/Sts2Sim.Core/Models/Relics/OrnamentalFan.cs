using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class OrnamentalFan : RelicModel
{
    private int _attacksThisTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _attacksThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }

        _attacksThisTurn++;
        if (_attacksThisTurn % 3 != 0)
        {
            return;
        }

        await CreatureCmd.GainBlock(
            Owner.Creature.CombatState!,
            Owner.Creature,
            4m,
            ValueProp.Unpowered,
            null,
            cardPlay);
    }

    public override Task AfterCombatEnd()
    {
        _attacksThisTurn = 0;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_attacksThisTurn);
    }
}
