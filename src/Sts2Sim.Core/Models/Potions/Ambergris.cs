using Sts2Sim.Core.Combat;
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

        // Native: CombatManager.Instance.IsInProgress. A combat-layout event's prepared combat has registered the player
        // but has no running engine yet, so IsLiveCombat() (true when engine-less) would wrongly grant the extra turn.
        if (target.CombatState is CombatState { Engine: { IsInProgress: true } } combatState &&
            combatState.ContainsCreature(target))
        {
            await PowerCmd.Apply<ExtraTurnPower>(
                combatState,
                target,
                1m,
                Owner.Creature,
                cardSource: null);
        }
    }
}
