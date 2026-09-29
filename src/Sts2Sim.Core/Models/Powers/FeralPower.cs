using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class FeralPower : PowerModel
{
    private int _zeroCostAttacksPlayed;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int DisplayAmount => Math.Max(0, Amount - _zeroCostAttacksPlayed);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        // Native snapshots all zero-energy Attack CardPlayStarted entries this turn.
        _zeroCostAttacksPlayed = Owner.Player!.PlayerCombatState!.ZeroCostAttacksStartedThisTurn;
        return Task.CompletedTask;
    }

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card, bool isAutoPlay, ResourceInfo resources, CardLocation location)
    {
        if (!ReferenceEquals(card.Owner.Creature, Owner) || card.Type != CardType.Attack ||
            resources.EnergyValue > 0 || card.IsDupe || _zeroCostAttacksPlayed >= Amount)
            return location;
        return new CardLocation(Owner.Player!, PileType.Hand, CardPilePosition.Top);
    }

    public override Task AfterModifyingCardPlayResultLocation(CardModel card, CardLocation location)
    {
        _zeroCostAttacksPlayed++;
        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner)) _zeroCostAttacksPlayed = 0;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_zeroCostAttacksPlayed);
}
