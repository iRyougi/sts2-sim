using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>原版 v0.113.1 <c>Pocketwatch</c>：两个计数都在 <c>BeforeSideTurnStart</c> 换手，
/// <c>ModifyHandDraw</c> 只读上回合计数、无副作用。回合开始设置阶段（抽牌、<c>AfterPlayerTurnStart</c>，
/// 例如 Gambling Chip 弃掉的 Sly 牌）打出的牌因此计入本回合（#1043）。</summary>
public sealed class Pocketwatch : RelicModel
{
    private const int CardPlayThreshold = 3;
    private const int ExtraDraw = 3;

    private int _cardsPlayedThisTurn;
    private int _cardsPlayedLastTurn;

    internal int CardsPlayedThisTurn => _cardsPlayedThisTurn;
    internal int CardsPlayedPreviousTurn => _cardsPlayedLastTurn;
    /// <summary>此刻做手牌摸牌时 <see cref="ModifyHandDraw"/> 是否加牌（由回合数与上回合计数派生）。</summary>
    internal bool ShouldDrawExtra =>
        Owner.PlayerCombatState is { TurnNumber: not 1 } && _cardsPlayedLastTurn <= CardPlayThreshold;
    internal bool CanStillTriggerThisTurn => CardsPlayedThisTurn <= CardPlayThreshold;
    internal int CardPlayThresholdSnapshot => CardPlayThreshold;

    public int CardsPlayedThisTurnSnapshot => CardsPlayedThisTurn;
    public int CardsPlayedPreviousTurnSnapshot => CardsPlayedPreviousTurn;
    public bool ShouldDrawExtraSnapshot => ShouldDrawExtra;
    public bool CanStillTriggerThisTurnSnapshot => CanStillTriggerThisTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        // 原版另查 CombatManager.IsInProgress：它只在出牌动作之后的 CheckWinCondition / EndCombatInternal 才变假，
        // 出牌结算途中恒为真，模拟器不需要单独的判断。
        if (ReferenceEquals(cardPlay.Card.Owner, Owner))
        {
            _cardsPlayedThisTurn++;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal originalCardCount)
    {
        if (player != Owner || !ShouldDrawExtra)
        {
            return originalCardCount;
        }

        return originalCardCount + ExtraDraw;
    }

    public override Task BeforeSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _cardsPlayedLastTurn = _cardsPlayedThisTurn;
            _cardsPlayedThisTurn = 0;
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnStart(
        CombatSide side,
        IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _cardsPlayedThisTurn = 0;
        _cardsPlayedLastTurn = 0;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_cardsPlayedThisTurn);
        builder.Append(_cardsPlayedLastTurn);
    }

}
