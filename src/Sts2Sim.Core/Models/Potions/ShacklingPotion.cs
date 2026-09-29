using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class ShacklingPotion : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AllEnemies;

    protected override async Task OnUse(Creature? target)
    {
        _ = target;
        ICombatState combatState = Owner.Creature.CombatState!;

        foreach (Creature enemy in combatState.HittableEnemies.ToArray())
            await PowerCmd.Apply<ShacklingPotionPower>(
                combatState,
                enemy,
                7m,
                Owner.Creature,
                null);
    }
}
