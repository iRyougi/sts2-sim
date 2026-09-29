using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Potions;

public sealed class ExplosiveAmpoule : PotionModel
{
    private const decimal Damage = 10m;

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AllEnemies;

    protected override async Task OnUse(Creature? target)
    {
        ICombatState combatState = Owner.Creature.CombatState!;
        await CreatureCmd.Damage(
            combatState,
            combatState.HittableEnemies.ToArray(),
            Damage,
            ValueProp.Unpowered,
            Owner.Creature,
            cardSource: null,
            cardPlay: null);
    }
}
