using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

internal interface ICombatObserver
{
    void CombatStarted(CombatState state);

    void PlayerTurnPrepared(CombatState state)
    {
    }

    void PlayerTurnStarted(CombatState state);

    void CardDrawn(CardModel card);

    void CardPlayStarted(CardModel card, Creature? target);

    void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay);

    void CardPlayAborted(CardModel card, Creature? target)
    {
    }

    // After player cleanup and before enemy start hooks: read-only half-turn boundary.
    void EnemyTurnStarting(CombatState state)
    {
    }

    void EnemyMoveStarted(Creature source, string moveId);

    void EnemyMoveFinished(Creature source, string moveId);

    void EnemyMoveAborted(Creature source, string moveId)
    {
    }

    void DamageResolved(Creature? dealer, DamageResult result)
    {
    }

    // Read-only validation notification; reports the committed power delta before
    // AfterPowerAmountChanged listeners run. Existing report writers need not implement it.
    void PowerAmountChanged(PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
    }

    // Read-only duration diagnostics; existing reporting observers retain no-op defaults.
    void PowerDurationTick(PowerModel power)
    {
    }

    void SideTurnEndListener(string phase, AbstractModel listener, CombatSide side, bool starting)
    {
    }

    void PlayerTurnEnded(CombatState state);

    void PotionUseStarted(PotionModel potion, Creature? target);

    void PotionUseFinished(PotionModel potion, Creature? target);

    void PotionUseAborted(PotionModel potion, Creature? target)
    {
    }
}
