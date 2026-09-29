using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>Grants 1 Intangible to the targeted player. Silent-specific potion (Silent4 epoch).</summary>
public sealed class GhostInAJar : PotionModel
{
    private const decimal IntangibleGranted = 1m;

    public override PotionRarity Rarity => PotionRarity.Rare;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<IntangiblePower>(
            target.CombatState!, target, IntangibleGranted, Owner.Creature, cardSource: null);
    }
}
