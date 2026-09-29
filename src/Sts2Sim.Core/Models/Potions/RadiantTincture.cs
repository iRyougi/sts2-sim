using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class RadiantTincture : PotionModel
{
    private const decimal ImmediateEnergy = 1m;
    private const decimal RadianceDuration = 3m;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Player!.PlayerCombatState!.GainEnergy(ImmediateEnergy);
        await PowerCmd.Apply<RadiancePower>(
            target.CombatState!,
            target,
            RadianceDuration,
            applier: null,
            cardSource: null);
    }
}
