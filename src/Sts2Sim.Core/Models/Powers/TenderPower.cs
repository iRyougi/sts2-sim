namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

public sealed class TenderPower : PowerModel
{
    private int _cardsPlayedThisTurn;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public int DisplayAmount => _cardsPlayedThisTurn;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!ReferenceEquals(cardPlay.Card.Owner, Owner.Player))
        {
            return;
        }

        _cardsPlayedThisTurn++;
        await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, -1m, Applier, null);
        await PowerCmd.Apply<DexterityPower>(Owner.CombatState!, Owner, -1m, Applier, null);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) => builder.Append(_cardsPlayedThisTurn);

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner) || _cardsPlayedThisTurn == 0)
        {
            return;
        }

        await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, _cardsPlayedThisTurn, Applier, null);
        await PowerCmd.Apply<DexterityPower>(Owner.CombatState!, Owner, _cardsPlayedThisTurn, Applier, null);
        _cardsPlayedThisTurn = 0;
    }
}
