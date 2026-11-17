using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MeatOnTheBone : RelicModel
{
    private const decimal HpThreshold = 50m; // DynamicVar "HpThreshold"

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task BeforeCombatStart()
    {
        if (WillHealOnCombatFinished())
        {
            Status = RelicStatus.Active;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (creature != Owner.Creature)
        {
            return Task.CompletedTask;
        }

        if (Owner.Creature.CombatState?.IsLiveCombat() != true)
        {
            return Task.CompletedTask;
        }

        Status = WillHealOnCombatFinished() ? RelicStatus.Active : RelicStatus.Normal;
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictoryEarly()
    {
        if (!Owner.Creature.IsAlive ||
            Owner.Creature.CurrentHp > Owner.Creature.MaxHp * 0.5m)
        {
            return Task.CompletedTask;
        }

        Status = RelicStatus.Normal;
        return CreatureCmd.Heal(Owner.Creature, 12m);
    }

    // Source MeatOnTheBone.WillHealOnCombatFinished, used only to drive Status.
    private bool WillHealOnCombatFinished()
    {
        Creature creature = Owner.Creature;
        int num = (int)((decimal)creature.MaxHp * (HpThreshold / 100m));
        return creature.CurrentHp <= num;
    }
}
