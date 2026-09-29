using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Combat;

/// <summary>
/// 战斗态接口。偏离 #37：成员按需求增长,目前只到 Task 14 的 <c>CombatEngine</c> 实际用到的最小面——
/// 游戏原版还有 Modifiers/BadgeModels/MultiplayerScalingModel 等,均未移植。
/// </summary>
public interface ICombatState
{
    IEnumerable<AbstractModel> IterateHookListeners();

    IRunState RunState { get; }

    IReadOnlyList<Creature> Allies { get; }

    // Native filters IsPlayer. Model-less standalone creatures are test-only player proxies;
    // retaining them here preserves the minimal ICombatState fixture contract without admitting monster-backed pets.
    IReadOnlyList<Creature> PlayerCreatures =>
        Allies.Where(creature => creature.IsPlayer || (!creature.IsMonster && !creature.IsPet)).ToList();

    IReadOnlyList<Creature> Enemies { get; }

    IReadOnlyList<Creature> Creatures { get; }

    IReadOnlyList<Creature> EscapedCreatures => Array.Empty<Creature>();

    IReadOnlyList<Player> Players { get; }

    IReadOnlyList<Creature> HittableEnemies { get; }

    CombatDamageHistory DamageHistory => CombatDamageHistory.Empty;

    ICardSelectionDecisionSource CardSelectionSource => RejectingCardSelectionDecisionSource.Instance;

    CombatSide CurrentSide { get; set; }

    int RoundNumber { get; set; }

    Creature CreateCreature(MonsterModel monster, CombatSide side, string? slotName) =>
        throw new NotSupportedException(
            $"{GetType().Name} does not support creating monsters dynamically.");

    void AddCreature(Creature creature) =>
        throw new NotSupportedException(
            $"{GetType().Name} does not support adding creatures dynamically.");

    Creature AddMonster(MonsterModel monster, CombatSide side, string? slotName)
    {
        Creature creature = CreateCreature(monster, side, slotName);
        AddCreature(creature);
        return creature;
    }

    void CreatureEscaped(Creature creature) =>
        throw new NotSupportedException($"{GetType().Name} does not support creature escape.");

    void RemoveCreature(Creature creature, bool unattach = true) =>
        throw new NotSupportedException($"{GetType().Name} does not support creature removal.");

    IReadOnlyList<Creature> GetOpponentsOf(Creature creature);

    IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side);

    bool ContainsCreature(Creature creature);

    bool IsLiveCombat();

    /// <summary><c>CombatManager.IsOverOrEnding</c>：战斗已结束或正在结束（胜负已定但尚未结算）。
    /// 默认视为仍在进行；只有挂着引擎的 <see cref="CombatState"/> 会真正判定。</summary>
    bool IsOverOrEnding() => false;
}
