using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class CureAll : PotionModel
{
    private const decimal EnergyGranted = 1m;
    private const int CardsDrawn = 2;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Player!.PlayerCombatState!.GainEnergy(EnergyGranted);
        await CardPileCmd.Draw(
            target.CombatState!,
            CardsDrawn,
            target.Player,
            fromHandDraw: false);
    }
}
