using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Act 1 event offering a dangerous shortcut or a rest followed by four Wrigglers.
/// 偏离 #148：不移植 DynamicVars，金币奖励保存在私有字段；#150 的 IsAllowed predicate 已在此实现，事件池选择由 Task 3 集成。
/// 四怪战斗使用偏离 #37/#47 的显式批量 CombatRoom；复用 Monster 普通奖励入口，
/// 仍不建模 EncounterModel 的场景与专属元数据。</summary>
public sealed class DenseVegetation : EventModel
{
    public override bool GenerateForcedCombatRewards => true;

    public override bool IsAllowed(IRunState runState) =>
        runState.Players.Count == 1 ||
        runState.Players.All(player => player.Creature.CurrentHp > 8m);

    private decimal _gold;

    protected override void CalculateVars() => _gold = Rng.NextInt(61, 100);

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        new EventOption("TRUDGE_ON", TrudgeOnAsync),
        new EventOption("REST", RestAsync),
    };

    private async Task TrudgeOnAsync()
    {
        await CreatureCmd.Damage(
            RunState,
            Owner.Creature,
            8m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        await PlayerCmd.GainGold(_gold, Owner);
        Finish();
    }

    private async Task RestAsync()
    {
        await PlayerCmd.MimicRestSiteHeal(Owner, playSfx: false);
        SetOptions(new[] { new EventOption("FIGHT", FightAsync) });
    }

    private Task FightAsync()
    {
        RequestForcedCombatSlottedBatch(CreateWrigglers);
        Finish();
        return Task.CompletedTask;
    }

    // Native DenseVegetationEventEncounter.GenerateMonsters: one unstunned Wriggler per slot
    // wriggler1..wriggler4; the Wriggler opening move is chosen by these slot names.
    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateWrigglers() =>
        Enumerable.Range(1, 4)
            .Select(index => ((MonsterModel)ModelDb.Monster<Wriggler>().MutableClone(), (string?)$"wriggler{index}"))
            .ToList()
            .AsReadOnly();
}
