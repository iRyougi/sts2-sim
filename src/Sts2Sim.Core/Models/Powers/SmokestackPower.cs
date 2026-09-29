using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class SmokestackPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card.Type == CardType.Status && creator?.Creature == Owner)
            await CreatureCmd.Damage(Owner.CombatState!, Owner.CombatState!.HittableEnemies,
                Amount, ValueProp.Unpowered, Owner, null, null);
    }
}
