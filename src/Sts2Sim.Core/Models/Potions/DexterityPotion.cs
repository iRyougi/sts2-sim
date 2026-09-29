using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class DexterityPotion : PotionModel
{
    private const decimal DexterityGranted = 2m;

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<DexterityPower>(
            target.CombatState!,
            target,
            DexterityGranted,
            applier: null,
            cardSource: null);
    }
}
