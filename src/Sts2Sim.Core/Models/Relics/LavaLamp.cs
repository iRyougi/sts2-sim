using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LavaLamp : RelicModel
{
    private bool _tookDamageThisCombat;

    public override RelicRarity Rarity => RelicRarity.Shop;

    public override Task AfterRoomEntered(AbstractRoom room)
    {
        _tookDamageThisCombat = false;
        return Task.CompletedTask;
    }

    public override Task AfterDamageReceived(
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (Owner.RunState.CurrentRoom is CombatRoom &&
            ReferenceEquals(target, Owner.Creature) &&
            result.UnblockedDamage > 0 &&
            !props.HasFlag(ValueProp.Unblockable))
        {
            _tookDamageThisCombat = true;
        }

        return Task.CompletedTask;
    }

    public override CardModel? TryModifyCardRewardOptionLate(
        IRunState runState,
        Player player,
        CardModel option)
    {
        if (Owner.RunState.CurrentRoom is not CombatRoom ||
            !ReferenceEquals(player, Owner) ||
            _tookDamageThisCombat ||
            !option.IsUpgradable)
        {
            return null;
        }

        var upgraded = (CardModel)option.MutableClone();
        CardCmd.Upgrade(upgraded);
        return upgraded;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_tookDamageThisCombat);
    }
}
