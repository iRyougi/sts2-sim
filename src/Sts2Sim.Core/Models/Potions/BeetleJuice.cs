using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class BeetleJuice : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<ShrinkPower>(
            Owner.Creature.CombatState!,
            target,
            4m,
            Owner.Creature,
            cardSource: null);
    }
}
