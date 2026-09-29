namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

public sealed class SwipePower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public CardModel? StolenCard { get; private set; }

    public async Task Steal(CardModel card)
    {
        StolenCard = card.DeckVersion
            ?? throw new ArgumentException("Only a persistent-deck combat copy can be stolen.", nameof(card));
        await CardPileCmd.RemoveFromDeckForTheft(StolenCard.Owner, StolenCard);
    }

    public override Task BeforeDeath(Creature target)
    {
        // 投影里不得触碰真实 CombatRoom：克隆共享 RunState，AddExtraReward 会拿克隆玩家
        // 去比对真实战斗的玩家列表并抛异常。搜索只需要知道“窃牌还没还回来”，
        // 这个信息已由 StolenCard 非空表达并进入指纹（见 AppendCombatStateDescription）。
        if ((Owner.CombatState as CombatState)?.IsProjection == true)
        {
            return Task.CompletedTask;
        }

        if (target != Owner || StolenCard is null) return Task.CompletedTask;
        CombatRoom room = Owner.CombatState?.RunState.CurrentRoom as CombatRoom
            ?? throw new InvalidOperationException("SwipePower can only return a card from a combat room.");
        room.AddExtraReward(StolenCard.Owner, new SpecialCardReward(StolenCard, StolenCard.Owner));
        StolenCard = null;
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        CardModel? sourceCard = ((SwipePower)source).StolenCard;
        if (sourceCard is null)
        {
            StolenCard = null;
            return;
        }

        if (cardMap.TryGetValue(sourceCard, out CardModel? clonedCard))
        {
            StolenCard = clonedCard;
            return;
        }

        Entities.Players.Player clonedOwner = creatureMap[sourceCard.Owner.Creature].Player!;
        StolenCard = sourceCard.CloneForCombat(clonedOwner);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(StolenCard is not null);
        if (StolenCard is null)
        {
            return;
        }

        builder.Append(context.State.Players.ToList().IndexOf(StolenCard.Owner));
        context.AppendDetachedCard(ref builder, StolenCard);
    }
}
