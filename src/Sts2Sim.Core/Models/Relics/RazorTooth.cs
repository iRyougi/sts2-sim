using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class RazorTooth : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner &&
            cardPlay.Card.Type is CardType.Attack or CardType.Skill &&
            cardPlay.Card.IsUpgradable)
        {
            cardPlay.Card.Upgrade();
        }

        return Task.CompletedTask;
    }
}
