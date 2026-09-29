using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>OblivionPower</c>（按施加者分实例）：施加者每打出一张牌，出牌结束后给持有者施加
/// 出牌开始时的层数的 <see cref="DoomPower"/>（施加者为本能力的施加者）；玩家回合结束时移除。
/// 出牌开始时记下的层数让打出 Oblivion 本身不会触发新施加的实例。</summary>
public sealed class OblivionPower : PowerModel
{
    // 原版 InternalData：MutableClone 时重新初始化为空；战斗克隆由 RestoreCombatCloneReferencesFrom 按映射复制。
    private Dictionary<CardModel, int> _amountsForPlayedCards = new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (Applier?.Player is null)
            return Task.CompletedTask;
        if (cardPlay.Card.Owner != Applier.Player)
            return Task.CompletedTask;

        _amountsForPlayedCards.Add(cardPlay.Card, Amount);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (_amountsForPlayedCards.Remove(cardPlay.Card, out int amount))
            await PowerCmd.Apply<DoomPower>(Owner.CombatState!, Owner, amount, Applier, null);
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player)
            await PowerCmd.Remove(this);
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _amountsForPlayedCards = new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
    }

    internal override IEnumerable<CardModel> EnumerateCombatCloneCards() => _amountsForPlayedCards.Keys;

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        _amountsForPlayedCards = new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
        foreach ((CardModel card, int amount) in ((OblivionPower)source)._amountsForPlayedCards)
            _amountsForPlayedCards.Add(cardMap[card], amount);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        context.AppendCardReferenceValues(ref builder, _amountsForPlayedCards);
}
