using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

public sealed class DaughterOfTheWind : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task AfterCardPlayed(CardPlay cardPlay) =>
        cardPlay.Card.Type == CardType.Attack &&
        ReferenceEquals(cardPlay.Card.Owner, Owner)
            ? CreatureCmd.GainBlock(
                Owner.Creature.CombatState!,
                Owner.Creature,
                1m,
                ValueProp.Unpowered,
                null,
                null)
            : Task.CompletedTask;
}
