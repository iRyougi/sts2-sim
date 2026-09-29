using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Potions;

public sealed class Ambergris : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Event;
    public override PotionUsage Usage => PotionUsage.AnyTime;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        await CreatureCmd.Heal(target, target.MaxHp * 0.5m);

        if (target.CombatState is { } combatState &&
            combatState.IsLiveCombat() &&
            combatState.ContainsCreature(target))
        {
            await PowerCmd.Apply<AmbergrisPower>(
                combatState,
                target,
                1m,
                target,
                cardSource: null);
        }
    }
}
