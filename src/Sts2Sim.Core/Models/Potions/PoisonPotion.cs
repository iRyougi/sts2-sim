using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>Applies 6 Poison to the targeted enemy. Silent-specific potion (Silent4 epoch).</summary>
public sealed class PoisonPotion : PotionModel
{
    private const decimal PoisonApplied = 6m;

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<PoisonPower>(
            target.CombatState!, target, PoisonApplied, Owner.Creature, cardSource: null);
    }
}
