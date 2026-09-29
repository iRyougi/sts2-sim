using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class GamePiece : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player != Owner ||
            cardPlay.Card.Type != CardType.Power)
        {
            return;
        }

        await CardPileCmd.Draw(
            Owner.Creature.CombatState!,
            1,
            Owner,
            fromHandDraw: false);
    }
}
