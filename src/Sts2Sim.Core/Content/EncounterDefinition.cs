using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Content;

/// <summary>遭遇战定义：可重复生成带槽位怪物批次的工厂、遭遇标签和 Weak 身份。偏离 #46：不是
/// <c>AbstractModel</c> 子类（同 <see cref="ActDefinition"/>）；偏离 #47 已解决：支持任意数量的
/// 怪物和槽位。</summary>
public sealed class EncounterDefinition
{
    private readonly Func<Rng?, IReadOnlyList<(MonsterModel Monster, string? SlotName)>> _monsterBatchFactory;

    public const string DefaultName = "custom-encounter";

    public string Name { get; }

    private readonly string? _idEntry;

    /// <summary>原版遭遇的 <c>ModelId.Entry</c>，例如 <c>RUBY_RAIDERS_NORMAL</c>。原版用它派生怪物生成种子。
    /// 默认由名称生成 slug；第一幕 Overgrowth 早期移植的遭遇沿用了与原版类名不同的 <see cref="Name"/>，
    /// 在定义处显式指定。<see cref="Name"/> 仍是模拟器内部的遭遇标识，不随之改名。</summary>
    public string IdEntry
    {
        get => _idEntry ?? StringHelper.Slugify(Name);
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _idEntry = value;
        }
    }

    public IReadOnlyList<EncounterTag> Tags { get; }

    public bool IsWeak { get; }

    private IReadOnlyList<string> _slots = Array.Empty<string>();

    /// <summary>Authoritative encounter slot order, snapshotted for sharing across combats.</summary>
    public IReadOnlyList<string> Slots
    {
        get => _slots;
        init => _slots = Array.AsReadOnly(value.ToArray());
    }

    /// <summary>Optional encounter-specific bounds; null retains the room's Poverty-adjusted default.</summary>
    public int? MinGoldReward { get; init; }

    public int? MaxGoldReward { get; init; }

    public Func<CombatState, float>? GoldProportionCalculator { get; init; }

    public float CalculateGoldProportion(CombatState combatState) =>
        GoldProportionCalculator?.Invoke(combatState) ?? CalculateDefaultGoldProportion(combatState);

    internal static float CalculateDefaultGoldProportion(CombatState combatState) =>
        1f - (float)combatState.EscapedCreatures.Count / combatState.SpawnedEnemies.Count;

    public EncounterDefinition(
        Func<MonsterModel> monsterFactory,
        IReadOnlyList<EncounterTag>? tags = null,
        bool isWeak = false)
        : this(monsterFactory, tags, isWeak, name: null)
    {
    }

    public EncounterDefinition(
        Func<MonsterModel> monsterFactory,
        IReadOnlyList<EncounterTag>? tags,
        bool isWeak,
        string? name)
    {
        ArgumentNullException.ThrowIfNull(monsterFactory);
        _monsterBatchFactory = _ => new[] { (monsterFactory(), (string?)null) };
        Name = SnapshotName(name);
        Tags = SnapshotTags(tags);
        IsWeak = isWeak;
    }

    public EncounterDefinition(
        Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>> monsterBatchFactory,
        IReadOnlyList<EncounterTag>? tags = null,
        bool isWeak = false)
        : this(monsterBatchFactory, tags, isWeak, name: null)
    {
    }

    public EncounterDefinition(
        Func<IReadOnlyList<(MonsterModel Monster, string? SlotName)>> monsterBatchFactory,
        IReadOnlyList<EncounterTag>? tags,
        bool isWeak,
        string? name)
    {
        ArgumentNullException.ThrowIfNull(monsterBatchFactory);
        _monsterBatchFactory = _ => monsterBatchFactory();
        Name = SnapshotName(name);
        Tags = SnapshotTags(tags);
        IsWeak = isWeak;
    }

    public EncounterDefinition(
        Func<Rng, IReadOnlyList<(MonsterModel Monster, string? SlotName)>> monsterBatchFactory,
        IReadOnlyList<EncounterTag>? tags = null,
        bool isWeak = false)
        : this(monsterBatchFactory, tags, isWeak, name: null)
    {
    }

    public EncounterDefinition(
        Func<Rng, IReadOnlyList<(MonsterModel Monster, string? SlotName)>> monsterBatchFactory,
        IReadOnlyList<EncounterTag>? tags,
        bool isWeak,
        string? name)
    {
        ArgumentNullException.ThrowIfNull(monsterBatchFactory);
        _monsterBatchFactory = rng => monsterBatchFactory(
            rng ?? throw new InvalidOperationException(
                "This encounter requires an RNG to generate its monster batch."));
        Name = SnapshotName(name);
        Tags = SnapshotTags(tags);
        IsWeak = isWeak;
    }

    public IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateMonsters() =>
        CreateMonstersCore(rng: null);

    public IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateMonsters(Rng rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        return CreateMonstersCore(rng);
    }

    private IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateMonstersCore(Rng? rng)
    {
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> generated = _monsterBatchFactory(rng)
            ?? throw new InvalidOperationException("Monster batch factory returned null.");
        if (generated.Any(entry => entry.Monster is null))
        {
            throw new InvalidOperationException("Encounter monster batches cannot contain null.");
        }

        return Array.AsReadOnly(generated
            .Select(entry =>
            {
                MonsterModel monster = entry.Monster.IsMutable
                    ? entry.Monster
                    : (MonsterModel)entry.Monster.MutableClone();
                return (monster, entry.SlotName);
            })
            .ToArray());
    }

    /// <summary>兼容既有单怪物调用点。</summary>
    public MonsterModel CreateMonster()
    {
        IReadOnlyList<(MonsterModel Monster, string? SlotName)> monsters = CreateMonsters();
        if (monsters.Count == 0)
        {
            throw new InvalidOperationException("Encounter generated no monsters for the singular compatibility call.");
        }

        return monsters[0].Monster;
    }

    private static string SnapshotName(string? name)
    {
        if (name is null)
        {
            return DefaultName;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name;
    }

    private static IReadOnlyList<EncounterTag> SnapshotTags(IReadOnlyList<EncounterTag>? tags) =>
        tags is null || tags.Count == 0
            ? Array.Empty<EncounterTag>()
            : Array.AsReadOnly(tags.ToArray());
}
