using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Potions;

public sealed class PotionShapedRock : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Token;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return CreatureCmd.Damage(target.CombatState!, [target], 15m, ValueProp.Unpowered, Owner.Creature, null, null);
    }
}
