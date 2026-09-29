using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>回合结束弃手牌后，从保留的、非 X 费且本地费用大于 0 的牌里用 CombatCardSelection 流随机选一张，
/// 费用 -1 直到打出。</summary>
public sealed class Bookmark : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterFlush(Player player, IReadOnlyCollection<CardModel> flushedCards,
        IReadOnlyCollection<CardModel> retainedCards)
    {
        if (player != Owner)
            return Task.CompletedTask;

        List<CardModel> candidates = retainedCards
            .Where(card => !card.CostsXEnergy && card.LocalEnergyCost > 0)
            .ToList();
        if (candidates.Count == 0)
            return Task.CompletedTask;

        Owner.RunState.Rng.CombatCardSelection.NextItem(candidates)?.AddEnergyCostUntilPlayed(-1);
        return Task.CompletedTask;
    }
}
