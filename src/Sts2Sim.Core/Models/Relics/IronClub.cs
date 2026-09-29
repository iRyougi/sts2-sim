using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class IronClub : RelicModel
{
    private int _cardsPlayed;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner) return;
        _cardsPlayed++;
        if (_cardsPlayed < 4 || Owner.Creature.CombatState is null) return;
        _cardsPlayed = 0;
        if (Owner.Creature.CombatState is not null)
            await CardPileCmd.Draw(Owner.Creature.CombatState, 1, Owner, fromHandDraw: false);
    }
    internal override void AppendCombatStateDescription(ref Combat.StateDescription.CombatStateDescriptionBuilder builder, Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_cardsPlayed % 4);
}
