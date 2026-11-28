using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class InsatiableDominionPower : PowerModel
{
    private const int BaseDamage = 2;

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public int Damage => BaseDamage + (Owner?.Player?.PlayerCombatState?.TurnNumber ?? 0);

    public override int DisplayAmount => Damage;

    public override async Task AfterSideTurnEndLate(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await CreatureCmd.Damage(
                Owner.CombatState!,
                [Owner],
                Damage,
                ValueProp.Unpowered,
                dealer: null,
                cardSource: null,
                cardPlay: null);
        }
    }
}
