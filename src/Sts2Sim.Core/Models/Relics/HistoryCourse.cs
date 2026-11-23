using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HistoryCourse : RelicModel
{
    private CardModel? _lastAttack;
    private int _lastAttackTurn;
    private CardModel? _previousAttack;
    private int _previousAttackTurn;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Player, Owner) &&
            !cardPlay.Card.IsDupe &&
            cardPlay.Card.Type == CardType.Attack)
        {
            int turn = Owner.PlayerCombatState!.TurnNumber;
            if (_lastAttackTurn != turn)
            {
                // Draw-triggered attacks can finish before this turn's AutoPrePlay phase.
                _previousAttack = _lastAttack;
                _previousAttackTurn = _lastAttackTurn;
            }
            _lastAttack = cardPlay.Card;
            _lastAttackTurn = turn;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterAutoPrePlayPhaseEntered(Player player)
    {
        if (!ReferenceEquals(player, Owner) ||
            Owner.PlayerCombatState!.TurnNumber == 1)
        {
            return;
        }

        int previousTurn = Owner.PlayerCombatState.TurnNumber - 1;
        CardModel? attack = _lastAttackTurn == previousTurn
            ? _lastAttack
            : _previousAttackTurn == previousTurn ? _previousAttack : null;
        if (attack is null)
        {
            return;
        }

        await AutoPlayCmd.FromCards(
            Owner.Creature.CombatState!,
            Owner,
            [attack.CreateDupe(Owner)]);
    }

    public override Task AfterCombatEnd()
    {
        _lastAttack = null;
        _lastAttackTurn = 0;
        _previousAttack = null;
        _previousAttackTurn = 0;
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        if (source is not HistoryCourse sourceCourse)
        {
            _lastAttack = null;
            _previousAttack = null;
            return;
        }

        _lastAttack = CloneAttack(sourceCourse._lastAttack);
        _previousAttack = ReferenceEquals(sourceCourse._previousAttack, sourceCourse._lastAttack)
            ? _lastAttack
            : CloneAttack(sourceCourse._previousAttack);

        CardModel? CloneAttack(CardModel? sourceAttack)
        {
            if (sourceAttack is null)
            {
                return null;
            }
            if (cardMap.TryGetValue(sourceAttack, out CardModel? mappedAttack))
            {
                return mappedAttack;
            }

            CardModel clonedAttack = sourceAttack.CloneForCombat(Owner);
            clonedAttack.RestoreCombatCloneReferencesFrom(sourceAttack, cardMap);
            return clonedAttack;
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_lastAttackTurn);
        AppendAttack(ref builder, context, _lastAttack);
        builder.Append(_previousAttackTurn);
        AppendAttack(ref builder, context, _previousAttack);
        builder.Append(_lastAttack is not null && ReferenceEquals(_lastAttack, _previousAttack));
    }

    private static void AppendAttack(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context,
        CardModel? attack)
    {
        builder.Append(attack is not null);
        if (attack is not null)
        {
            bool isBound = attack.Pile is not null;
            builder.Append(isBound);
            if (isBound)
            {
                context.AppendCardReferences(ref builder, [attack]);
            }
            else
            {
                context.AppendDetachedCard(ref builder, attack);
            }
        }
    }
}
