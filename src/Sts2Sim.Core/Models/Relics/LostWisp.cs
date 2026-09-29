using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class LostWisp : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override async Task AfterCardPlayed(CardPlay play)
    {
        if (play.Card.Owner != Owner || play.Card.Type != CardType.Power) return;
        await CreatureCmd.Damage(
            Owner.Creature.CombatState!,
            Owner.Creature.CombatState!.HittableEnemies,
            8m,
            ValueProp.Unpowered,
            Owner.Creature,
            null,
            play);
    }
}