using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Reporting;

public interface IRunRecorder
{
    void BeginRun(RunState runState);

    void EnterFloor(MapPoint point, RoomType roomType);

    void RecordFloorDetail(FloorDetail detail);

    string BeginCombat(
        RunState runState,
        RoomType encounterType,
        string encounterName,
        CombatState combatState);

    void RecordTurnStart(CombatState combatState);

    void RecordDraw(CardModel card);

    void RecordCardPlay(
        CardPlay cardPlay,
        PlayerSnapshot before,
        PlayerSnapshot after,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter);

    void RecordPotionUse(
        PotionModel potion,
        Creature? target,
        PlayerSnapshot before,
        PlayerSnapshot after,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter);

    void RecordPotionUse(
        PotionModel potion,
        Creature? target,
        PlayerSnapshot before,
        PlayerSnapshot after,
        IReadOnlyList<EnemySnapshot> enemiesBefore,
        IReadOnlyList<EnemySnapshot> enemiesAfter,
        bool consumed,
        IReadOnlyList<string?> potionSlotsBefore,
        IReadOnlyList<string?> potionSlotsAfter) =>
        RecordPotionUse(potion, target, before, after, enemiesBefore, enemiesAfter);

    void RecordEnemyAction(
        Creature source,
        string moveId,
        IReadOnlyList<ActionSnapshotSegment> segments);

    void RecordEnemyAction(
        Creature source,
        string moveId,
        IReadOnlyList<ActionSnapshotSegment> segments,
        IReadOnlyList<DamageResult> resolvedDamage) =>
        RecordEnemyAction(source, moveId, segments);

    void RecordEndTurn(
        PlayerSnapshot finalPlayer,
        IReadOnlyList<EnemySnapshot> finalEnemies);

    void EndCombat(
        bool victory,
        PlayerSnapshot finalPlayer,
        IReadOnlyList<EnemySnapshot> finalEnemies,
        CombatRewards rewards);

    void ExitFloor();

    /// <param name="survived">结束时玩家是否存活；传 <c>null</c> 时按 <paramref name="finalHp"/> 推导。</param>
    void EndRun(
        bool won,
        int floorsVisited,
        int finalHp,
        bool reachedBoss = false,
        bool? survived = null,
        bool truncated = false,
        int actsCleared = 0);

    RunManifest BuildManifest();

    IReadOnlyDictionary<string, CombatLog> CombatLogs { get; }
}
