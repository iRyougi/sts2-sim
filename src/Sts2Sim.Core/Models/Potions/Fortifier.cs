using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Potions;

public sealed class Fortifier : PotionModel
{
    private const decimal CurrentBlockMultiplier = 2m;

    public override PotionRarity Rarity => PotionRarity.Uncommon;

    public override PotionUsage Usage => PotionUsage.CombatOnly;

    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        decimal blockToGain = target.Block * CurrentBlockMultiplier;
        if (blockToGain > 0m)
        {
            await CreatureCmd.GainBlock(
                target.CombatState!,
                target,
                blockToGain,
                ValueProp.Unpowered,
                cardSource: null,
                cardPlay: null);
        }
    }
}
