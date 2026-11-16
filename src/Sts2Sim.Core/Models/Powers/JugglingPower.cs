using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

public sealed class JugglingPower : PowerModel
{
    private int _attacksPlayedThisTurn;

    /// <summary>原版 v0.113.1 <c>Data.cardToClone</c>：本回合第 3 张攻击在 BeforeCardPlayed 记录，
    /// 该牌 AfterCardPlayed 时才复制，复制品反映打出后的实例状态（#922）。</summary>
    private CardModel? _cardToClone;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        _attacksPlayedThisTurn = Owner.Player!.PlayerCombatState!.AttackCardsStartedThisTurn;
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner != Owner.Player)
        {
            return Task.CompletedTask;
        }
        if (cardPlay.Card.Type != CardType.Attack)
        {
            return Task.CompletedTask;
        }
        _attacksPlayedThisTurn++;
        if (_attacksPlayedThisTurn == 3)
        {
            _cardToClone = cardPlay.Card;
        }
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (!ReferenceEquals(_cardToClone, cardPlay.Card))
        {
            return;
        }
        _cardToClone = null;
        for (int i = 0; i < Amount; i++)
        {
            CardModel clone = cardPlay.Card.CreateClone();
            await CardPileCmd.Generate(Owner.CombatState!, clone, PileType.Hand, Owner.Player);
        }
    }

    public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _attacksPlayedThisTurn = 0;
        }
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        CardModel? original = ((JugglingPower)source)._cardToClone;
        _cardToClone = original is not null && cardMap.TryGetValue(original, out CardModel? mapped) ? mapped : null;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context)
    {
        builder.Append(_attacksPlayedThisTurn);
        context.AppendCardReferences(ref builder, [_cardToClone]);
    }
}
