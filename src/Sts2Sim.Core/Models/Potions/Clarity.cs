using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class Clarity : PotionModel
{
    private const int ImmediateDraw = 1;
    private const decimal Duration = 3m;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await CardPileCmd.Draw(
            target.CombatState!,
            ImmediateDraw,
            target.Player!,
            fromHandDraw: false);
        await PowerCmd.Apply<ClarityPower>(
            target.CombatState!,
            target,
            Duration,
            applier: null,
            cardSource: null);
    }
}
