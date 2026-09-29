using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Potions;

public sealed class ShipInABottle : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ICombatState combatState = target.CombatState!;
        await CreatureCmd.GainBlock(
            combatState,
            target,
            10m,
            ValueProp.Unpowered,
            null,
            null);
        await PowerCmd.Apply<BlockNextTurnPower>(
            combatState,
            target,
            10m,
            Owner.Creature,
            null);
    }
}
