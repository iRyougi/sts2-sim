using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LizardTail : RelicModel
{
    private bool _wasUsed;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool IsUsedUp => _wasUsed;

    public override bool ShouldDieLate(Creature creature) =>
        creature != Owner.Creature ||
        _wasUsed;

    public override async Task AfterPreventingDeath(Creature creature)
    {
        _wasUsed = true;
        decimal amount = Math.Max(1m, creature.MaxHp * 0.5m);
        await CreatureCmd.Heal(creature, amount);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_wasUsed);
    }
}
