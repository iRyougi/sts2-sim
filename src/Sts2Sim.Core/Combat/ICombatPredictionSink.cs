using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>Passive notifications for combat prediction ledgers. Implementations must not mutate
/// combat state or consume RNG. Not copied by Clone: the owner re-attaches it on each branch.</summary>
public interface ICombatPredictionSink
{
    void HealApplied(Creature creature, decimal requested, int hpBefore, int hpAfter);
    void DeathPrevented(Creature target, AbstractModel preventer, int hpBefore, int hpAfter);
    void MaxHpLost(Creature creature, decimal amount, int maxHpBefore, int maxHpAfter, bool isFromCard);
    void PotionProcured(Player player, PotionModel potion);
}
