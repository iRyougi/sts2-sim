using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class OrnamentalFan : RelicModel
{
    private const int AttacksThreshold = 3; // CardsVar(3)

    private int _attacksThisTurn;

    // OrnamentalFan.UpdateDisplay (IsActivating is a 1s animation flag; always false at a decision point).
    private void UpdateStatus() =>
        Status = Owner.Creature.CombatState?.IsLiveCombat() != true
            ? RelicStatus.Normal
            : _attacksThisTurn % AttacksThreshold == AttacksThreshold - 1
                ? RelicStatus.Active
                : RelicStatus.Normal;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _attacksThisTurn = 0;
            UpdateStatus();
            Status = RelicStatus.Normal;
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
        UpdateStatus();
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
        // Source: IsActivating = false -> UpdateDisplay, with CombatManager.IsInProgress already false here -> Normal.
        // The attack counter is not cleared here; it is kept until the next combat's BeforeSideTurnStart (#1121).
        Status = RelicStatus.Normal;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_attacksThisTurn);
    }
}
