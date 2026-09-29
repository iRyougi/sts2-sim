using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Kusarigama : RelicModel
{
    private int _attacksThisTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCombatStart()
    {
        _attacksThisTurn = 0;
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

        ICombatState combatState = Owner.Creature.CombatState!;
        Creature? target = combatState.RunState.Rng.CombatTargets.NextItem(
            combatState.HittableEnemies);
        if (target is null)
        {
            return;
        }

        await CreatureCmd.Damage(
            combatState,
            new[] { target },
            6m,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            cardPlay);
    }

    public override Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _attacksThisTurn = 0;
        }

        return Task.CompletedTask;
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
