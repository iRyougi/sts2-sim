using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class FruitJuice : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.AnyTime;
    public override TargetType TargetType => TargetType.AnyPlayer;
    public override bool CanBeGeneratedInCombat => false;

    protected override Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return CreatureCmd.GainMaxHp(target, 5m);
    }
}
