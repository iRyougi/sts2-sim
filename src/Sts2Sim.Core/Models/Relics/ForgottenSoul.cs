using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>Deals one unpowered damage to a random hittable enemy whenever its owner exhausts a card.</summary>
public sealed class ForgottenSoul : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterCardExhausted(CardModel card, bool causedByEthereal)
    {
        if (!ReferenceEquals(card.Owner, Owner) || Owner.Creature.CombatState is not { } combatState)
        {
            return;
        }

        var target = Owner.RunState.Rng.CombatTargets.NextItem(combatState.HittableEnemies);
        if (target is not null)
        {
            await CreatureCmd.Damage(
                combatState,
                [target],
                1m,
                ValueProp.Unpowered,
                Owner.Creature,
                null,
                null);
        }
    }
}
