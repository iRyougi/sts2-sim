using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>On unblocked damage in combat, gain three block after the next block clear.</summary>
public sealed class SelfFormingClay : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        _ = props;
        _ = dealer;
        _ = cardSource;
        if (Owner.Creature.CombatState is not { } combatState ||
            !combatState.IsLiveCombat() ||
            !ReferenceEquals(target, Owner.Creature) || result.UnblockedDamage <= 0)
            return;
        await PowerCmd.Apply<SelfFormingClayPower>(
            combatState, Owner.Creature, 3m, Owner.Creature, null);
    }
}
