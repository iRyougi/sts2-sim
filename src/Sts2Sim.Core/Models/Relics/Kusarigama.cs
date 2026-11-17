using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class Kusarigama : RelicModel
{
    private const int AttacksThreshold = 3; // CardsVar(3)

    private int _attacksThisTurn;

    // Kusarigama.UpdateDisplay (IsActivating is a 1s animation flag; always false at a decision point).
    private void UpdateStatus() =>
        Status = _attacksThisTurn % AttacksThreshold == AttacksThreshold - 1
            ? RelicStatus.Active
            : RelicStatus.Normal;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeCombatStart()
    {
        _attacksThisTurn = 0;
        UpdateStatus();
        Status = RelicStatus.Normal;
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
        UpdateStatus();
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
            UpdateStatus();
            Status = RelicStatus.Normal;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        Status = RelicStatus.Normal;
        _attacksThisTurn = 0;
        UpdateStatus();
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_attacksThisTurn);
    }
}
