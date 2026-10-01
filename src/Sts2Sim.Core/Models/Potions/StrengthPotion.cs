using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

/// <summary>Applies 2 Strength to the targeted player in combat.</summary>
public sealed class StrengthPotion : PotionModel
{
    private const decimal StrengthGranted = 2m;

    public override PotionRarity Rarity => PotionRarity.Common;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await PowerCmd.Apply<StrengthPower>(target.CombatState!, target, StrengthGranted, applier: Owner.Creature, cardSource: null);
    }
}
