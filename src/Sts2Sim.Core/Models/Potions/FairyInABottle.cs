using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models.Potions;

public sealed class FairyInABottle : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.Automatic;
    public override TargetType TargetType => TargetType.Self;
    public override bool CanBeGeneratedInCombat => false;

    public override bool ShouldDie(Creature creature) => creature != Owner.Creature;

    public override Task AfterPreventingDeath(Creature creature) =>
        PotionCmd.UseAutomatic(this, creature);

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await CreatureCmd.Heal(target, Math.Max(1m, target.MaxHp * 0.30m));
    }
}
