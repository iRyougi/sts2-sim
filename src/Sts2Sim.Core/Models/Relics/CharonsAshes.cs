using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Deal three unpowered damage to hittable enemies when the owner's card exhausts.</summary>
public sealed class CharonsAshes : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        _ = causedByEthereal;
        if (!ReferenceEquals(card.Owner, Owner))
            return;
        var combatState = Owner.Creature.CombatState!;
        await CreatureCmd.Damage(combatState, combatState.HittableEnemies,
            3m, ValueProp.Unpowered, Owner.Creature, null, null);
    }
}
