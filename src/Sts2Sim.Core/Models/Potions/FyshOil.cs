using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class FyshOil : PotionModel
{
    private const decimal PowerGranted = 1m;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<StrengthPower>(
            target.CombatState!,
            target,
            PowerGranted,
            applier: Owner.Creature,
            cardSource: null);
        await PowerCmd.Apply<DexterityPower>(
            target.CombatState!,
            target,
            PowerGranted,
            applier: Owner.Creature,
            cardSource: null);
    }
}
