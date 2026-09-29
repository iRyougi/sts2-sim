using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class VulnerablePotion : PotionModel
{
    private const decimal VulnerableApplied = 3m;

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<VulnerablePower>(
            target.CombatState!,
            target,
            VulnerableApplied,
            applier: null,
            cardSource: null);
    }
}
