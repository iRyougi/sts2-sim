namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;

public sealed class EnragePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterCardPlayed(CardPlay cardPlay) =>
        cardPlay.Card.Type == CardType.Skill
            ? PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Amount, Owner, null)
            : Task.CompletedTask;
}
