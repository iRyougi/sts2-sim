using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class HistoryCourse : RelicModel
{
    private CardModel? _lastAttack;
    private int _lastAttackTurn;

    public override RelicRarity Rarity => RelicRarity.Event;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Player, Owner) &&
            !cardPlay.Card.IsDupe &&
            cardPlay.Card.Type == CardType.Attack)
        {
            _lastAttack = cardPlay.Card;
            _lastAttackTurn = Owner.PlayerCombatState!.TurnNumber;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterAutoPrePlayPhaseEntered(Player player)
    {
        if (!ReferenceEquals(player, Owner) ||
            Owner.PlayerCombatState!.TurnNumber == 1 ||
            _lastAttack is null ||
            _lastAttackTurn != Owner.PlayerCombatState.TurnNumber - 1)
        {
            return;
        }

        await AutoPlayCmd.FromCards(
            Owner.Creature.CombatState!,
            Owner,
            [_lastAttack.CreateDupe(Owner)]);
    }

    public override Task AfterCombatEnd()
    {
        _lastAttack = null;
        _lastAttackTurn = 0;
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        RelicModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap);
        if (source is not HistoryCourse { _lastAttack: { } sourceAttack })
        {
            _lastAttack = null;
            return;
        }

        if (cardMap.TryGetValue(sourceAttack, out CardModel? mappedAttack))
        {
            _lastAttack = mappedAttack;
            return;
        }

        _lastAttack = sourceAttack.CloneForCombat(Owner);
        _lastAttack.RestoreCombatCloneReferencesFrom(sourceAttack, cardMap);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_lastAttackTurn);
        builder.Append(_lastAttack is not null);
        if (_lastAttack is not null)
        {
            bool isBound = _lastAttack.Pile is not null;
            builder.Append(isBound);
            if (isBound)
            {
                context.AppendCardReferences(ref builder, [_lastAttack]);
            }
            else
            {
                context.AppendDetachedCard(ref builder, _lastAttack);
            }
        }
    }
}
