using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>v0.113.1 原版 <c>VelvetChoker</c>：+1 最大能量；本回合本玩家已开始的出牌（含进行中的嵌套出牌）
/// 达到 6 张后禁止打出。原版数 <c>History.CardPlaysStarted</c>，这里用同一时点（BeforeCardPlayed 之后）
/// 记录的 <see cref="PlayerCombatState.CardPlaysStartedThisTurn"/>。</summary>
public sealed class VelvetChoker : RelicModel
{
    private const int MaxCardsPerTurn = 6;

    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override decimal ModifyMaxEnergy(Player player, decimal amount) => player == Owner ? amount + 1m : amount;
    public override bool ShouldPlay(CardModel card, bool isAutoPlay) =>
        card.Owner != Owner || CardPlaysStartedThisTurn < MaxCardsPerTurn;

    private int CardPlaysStartedThisTurn => Owner.PlayerCombatState?.CardPlaysStartedThisTurn ?? 0;
}
