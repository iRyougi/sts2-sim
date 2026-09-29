using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ArtOfWar : RelicModel
{
    private bool _attackPlayedThisTurn;
    private bool _previousTurnHadNoAttack;

    internal bool CanTriggerNextTurn => !_attackPlayedThisTurn;

    public bool CanTriggerNextTurnSnapshot => CanTriggerNextTurn;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner &&
            cardPlay.Card.Type == CardType.Attack)
        {
            _attackPlayedThisTurn = true;
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
        {
            _previousTurnHadNoAttack = !_attackPlayedThisTurn;
            _attackPlayedThisTurn = false;
        }

        return Task.CompletedTask;
    }

    public override Task AfterEnergyReset(Player player)
    {
        if (player == Owner &&
            player.PlayerCombatState?.TurnNumber >= 1 &&
            _previousTurnHadNoAttack)
        {
            player.PlayerCombatState.GainEnergy(1m);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCombatEnd()
    {
        _attackPlayedThisTurn = false;
        _previousTurnHadNoAttack = false;
        return Task.CompletedTask;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_attackPlayedThisTurn);
        builder.Append(_previousTurnHadNoAttack);
    }
}
