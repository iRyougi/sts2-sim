using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>打出一张实际费用不低于 3 能量的自己的牌后，获得 1 能量。</summary>
public sealed class IvoryTile : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner && cardPlay.Resources.EnergyValue >= 3)
            await PlayerCmd.GainEnergy(1m, Owner);
    }
}
