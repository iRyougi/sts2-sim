using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 Shrine of the Warlord relic (#927): gain 2 Strength when the owner (or Osty) deals at least
/// 30 total damage with one powered attack hit.</summary>
public sealed class MightyWings : RelicModel
{
    private const decimal DamageThreshold = 30m;
    private const decimal Strength = 2m;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterDamageGiven(
        Creature? dealer,
        DamageResult result,
        ValueProp props,
        Creature target,
        CardModel? cardSource)
    {
        if (props.IsPoweredAttack() && dealer != null && (dealer == Owner.Creature || dealer == Owner.Osty) &&
            !(result.TotalDamage < DamageThreshold))
        {
            Flash();
            await PowerCmd.Apply<StrengthPower>(Owner.Creature.CombatState!, Owner.Creature, Strength, null, null);
        }
    }
}
