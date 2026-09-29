using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class EchoFormPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount)
    {
        if (!ReferenceEquals(card.Owner.Creature, Owner)) return playCount;
        // Native counts CardPlayStarted entries with IsFirstInSeries, including auto plays.
        int firstInSeries = Owner.Player!.PlayerCombatState!.FirstInSeriesCardPlaysStartedThisTurn;
        return firstInSeries < Amount ? playCount + 1 : playCount;
    }
}
