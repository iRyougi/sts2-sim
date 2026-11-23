namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Afflictions;

/// <summary>
/// Binds the first <see cref="PowerModel.Amount"/> cards the owner draws each turn with a <see cref="Bound"/> of
/// that same amount, and allows only one Bound card play per turn.
/// </summary>
/// <remarks>
/// 原版 <c>AfterCardDrawn</c> 数本回合（<c>HappenedThisTurn</c>，到换边才翻转）该玩家的 Bound <c>CardAfflictedEntry</c>。
/// 回合末 <c>BeforeSideTurnEnd</c> 之后、换边之前抽到的牌仍算在刚结束的回合里，不能另起计数（#1042）。
/// </remarks>
public sealed class ChainsOfBindingPower : PowerModel
{
    private bool _boundCardPlayedThisTurn;

    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        if (Owner.CombatState is not CombatState combat ||
            combat.CurrentSide != Owner.Side ||
            !ReferenceEquals(card.Owner, Owner.Player) ||
            card.Affliction is not null ||
            combat.SemanticHistory.CountThisTurn(
                combat, CombatSemanticHistory.ActorEvent.BoundAfflicted, Owner.Player!) >= Amount)
        {
            return;
        }

        await CardCmd.Afflict<Bound>(card, Amount);
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (!cardPlay.Card.IsDupe &&
            ReferenceEquals(cardPlay.Card.Owner.Creature, Owner) &&
            cardPlay.Card.Affliction is Bound)
        {
            _boundCardPlayedThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override bool ShouldPlay(CardModel card, bool isAutoPlay) =>
        !ReferenceEquals(card.Owner, Owner.Player) ||
        card.Affliction is not Bound ||
        !_boundCardPlayedThisTurn;

    public override Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (!participants.Contains(Owner))
        {
            return Task.CompletedTask;
        }

        _boundCardPlayedThisTurn = false;
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards))
        {
            if (card.Affliction is Bound)
            {
                CardCmd.ClearAffliction(card);
            }
        }

        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_boundCardPlayedThisTurn);
    }
}
