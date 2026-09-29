using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>Grants Strength when this power's owner is the player who generated a combat card.</summary>
public sealed class ArsenalPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardGenerated(CardModel card, Player? creator)
    {
        if (creator?.Creature == Owner)
        {
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner, Amount, Owner, null);
        }
    }
}
