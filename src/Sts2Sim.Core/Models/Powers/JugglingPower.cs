using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class JugglingPower : PowerModel
{
    private int _attacksPlayedThisTurn;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _attacksPlayedThisTurn = Owner.Player!.PlayerCombatState!.AttackCardsStartedThisTurn;
        return Task.CompletedTask;
    }

    public override async Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player || cardPlay.Card.Type != CardType.Attack)
        {
            return;
        }
        _attacksPlayedThisTurn++;
        if (_attacksPlayedThisTurn == 3)
        {
            for (int i = 0; i < Amount; i++)
            {
                CardModel clone = cardPlay.Card.CreateClone();
                await CardPileCmd.Generate(Owner.CombatState!, clone, PileType.Hand, Owner.Player);
            }
        }
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _attacksPlayedThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_attacksPlayedThisTurn);
}
