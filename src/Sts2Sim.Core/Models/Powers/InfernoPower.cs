using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class InfernoPower : PowerModel
{
    private decimal _selfDamage;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterPlayerTurnStart(Player player)
    {
        if (player == Owner.Player)
        {
            await CreatureCmd.Damage(Owner.CombatState!, [Owner], _selfDamage,
                ValueProp.Unblockable | ValueProp.Unpowered, Owner, null, null);
        }
    }

    public override Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || result.UnblockedDamage <= 0 ||
            Owner.CombatState!.CurrentSide != Owner.Side)
        {
            return Task.CompletedTask;
        }
        return CreatureCmd.Damage(Owner.CombatState, Owner.CombatState.HittableEnemies,
            Amount, ValueProp.Unpowered, Owner, null, null);
    }

    public void IncrementSelfDamage()
    {
        AssertMutable();
        _selfDamage++;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_selfDamage);
}
