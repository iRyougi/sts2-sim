using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>SicEmPower</c>（减益，挂在敌人身上）：施加者的 Osty 对持有者造成伤害后，
/// 施加者召唤层数；持有者所在一方回合结束时移除。</summary>
public sealed class SicEmPower : PowerModel
{
    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (dealer?.Monster is Osty &&
            dealer.PetOwner is { } petOwner &&
            Applier is not null &&
            petOwner.Creature == Applier &&
            target == Owner)
        {
            await OstyCmd.Summon(petOwner, Amount, this);
        }
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
            await PowerCmd.Remove(this);
    }
}
