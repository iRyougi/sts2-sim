using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Powers;

public sealed class NightmarePower : PowerModel
{
    /// <summary>原版 <c>Data.selectedCard</c>：选中牌的分离克隆（不在任何牌堆），抽牌前每份再 CreateClone 一次。</summary>
    private CardModel? _selectedCard;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    /// <summary>原版 v0.113.1 <c>SetSelectedCard</c>：CreateClone → 清苦难 → EndOfTurnCleanup（#922），
    /// 保留附魔、关键字、本场战斗费用等实例状态，去掉本回合临时状态。</summary>
    public void SetSelectedCard(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        CardModel clone = card.CreateClone();
        CardCmd.ClearAffliction(clone);
        clone.EndOfTurnCleanup();
        _selectedCard = clone;
    }

    public override async Task BeforeHandDraw(Player player)
    {
        if (player != Owner.Player)
        {
            return;
        }

        CardModel card = _selectedCard
            ?? throw new InvalidOperationException("NightmarePower has no selected card.");
        for (int index = 0; index < Amount; index++)
        {
            CardModel copy = card.CreateClone();
            await CardPileCmd.Generate(Owner.CombatState!, copy, PileType.Hand, Owner.Player);
        }

        await PowerCmd.Remove(this);
    }

    // 分离克隆不在任何牌堆，交给 CombatState.Clone 统一建映射（含其 CloneOf 来源链）。
    internal override IEnumerable<CardModel> EnumerateCombatCloneCards() =>
        _selectedCard is null ? [] : [_selectedCard];

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        CardModel? sourceCard = ((NightmarePower)source)._selectedCard;
        _selectedCard = sourceCard is null ? null : cardMap[sourceCard];
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_selectedCard is not null);
        if (_selectedCard is not null)
        {
            context.AppendDetachedCard(ref builder, _selectedCard);
        }
    }
}
