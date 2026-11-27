using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Exceptions;
using Sts2Sim.Core.Models.Afflictions;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models;

/// <summary>
/// 规范(canonical)模型注册表。与游戏一致保持 static:Hook 调度器无状态、注册表 Init 后只读,
/// 对并行模拟器是安全的共享常量数据(偏离 #18 有完整论证)。
/// 游戏用 source generator 枚举全部 AbstractModel 子类;模拟器改为显式类型列表 Init(偏离 #11)。
/// </summary>
public static class ModelDb
{
    private const int _initialCapacity = 4096;

    private static readonly Dictionary<ModelId, AbstractModel> _contentById = new(_initialCapacity);
    private static readonly ConcurrentDictionary<Type, ModelId> IdsByType = new();
    private static readonly ConcurrentDictionary<Type, (int Gen, AbstractModel Model)> ModelsByType = new();
    private static int _generation = 1;
    private static bool _defaultContentInitialized;
    private static int _orbsGeneration;
    private static ReadOnlyCollection<OrbModel>? _orbsCache;
    private static int _charactersGeneration;
    private static ReadOnlyCollection<CharacterModel>? _charactersCache;

    internal static int Generation => Volatile.Read(ref _generation);

    private static class TypeIdCache<T> where T : AbstractModel
    {
        public static readonly ModelId Id = GetId(typeof(T));
    }

    private static class ModelCache<T> where T : AbstractModel
    {
        public static int Generation;
        public static T? Model;
    }

    private static class AllCache<T> where T : AbstractModel
    {
        public static int Generation;
        public static ReadOnlyCollection<T>? Items;
    }

    /// <summary>Registers all concrete content types in <see cref="ContentRegistry.AllTypes"/> idempotently.</summary>
    public static void EnsureInitialized()
    {
        if (_defaultContentInitialized)
        {
            return;
        }

        Init(ContentRegistry.AllTypes);
        _defaultContentInitialized = true;
    }

    public static void Init(IEnumerable<Type> modelTypes)
    {
        foreach (Type type in modelTypes)
        {
            Inject(type);
        }
    }

    public static void Inject([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type type)
    {
        if (!typeof(AbstractModel).IsAssignableFrom(type) || type.IsAbstract)
        {
            throw new ArgumentException($"Type {type} is not a concrete AbstractModel subclass.", nameof(type));
        }
        ModelId id = GetId(type);
        if (_contentById.TryGetValue(id, out AbstractModel? existing))
        {
            if (existing.GetType() != type)
            {
                throw new InvalidOperationException($"ModelId collision: {id} is already registered by {existing.GetType()}, cannot register {type}.");
            }
            return; // 同类型重复 Init:幂等跳过
        }
        AbstractModel value = (AbstractModel)Activator.CreateInstance(type)!;
        _contentById[id] = value;
        Interlocked.Increment(ref _generation);
    }

    /// <summary>仅供测试清场使用(ModelDb 在生产路径上 Init 后只读)。</summary>
    public static void ResetForTests()
    {
        _defaultContentInitialized = false;
        _contentById.Clear();
        ModelsByType.Clear();
        Interlocked.Increment(ref _generation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ModelId GetId<T>() where T : AbstractModel => TypeIdCache<T>.Id;

    public static ModelId GetId(Type type)
    {
        // A pure function of the type's name and base chain (regex slugs): computed once per type (Plan 09-O).
        // ModelId is an immutable record compared by value, so handing out one shared instance changes nothing.
        return IdsByType.GetOrAdd(type, static t => new ModelId(GetCategory(t), GetEntry(t)));
    }

    public static Type GetCategoryType(Type type)
    {
        Type type2 = type;
        while (type2.BaseType != typeof(AbstractModel))
        {
            type2 = type2.BaseType!;
        }
        return type2;
    }

    public static string GetCategory(Type type)
    {
        return ModelId.SlugifyCategory(GetCategoryType(type).Name);
    }

    public static string GetEntry(Type type)
    {
        return StringHelper.Slugify(type.Name);
    }

    public static bool Contains(Type type)
    {
        return _contentById.ContainsKey(GetId(type));
    }

    public static T? GetByIdOrNull<T>(ModelId id) where T : AbstractModel
    {
        if (_contentById.TryGetValue(id, out AbstractModel? value))
        {
            return (T)value;
        }
        return null;
    }

    public static T GetById<T>(ModelId id) where T : AbstractModel
    {
        return GetByIdOrNull<T>(id) ?? throw new ModelNotFoundException(id);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static T Get<T>() where T : AbstractModel
    {
        int gen = Volatile.Read(ref _generation);
        if (Volatile.Read(ref ModelCache<T>.Generation) == gen && ModelCache<T>.Model is { } cached)
        {
            return cached;
        }

        return ResolveAndCache<T>(gen);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static T ResolveAndCache<T>(int gen) where T : AbstractModel
    {
        T model = (T)_contentById[TypeIdCache<T>.Id];
        ModelCache<T>.Model = model;
        Volatile.Write(ref ModelCache<T>.Generation, gen);
        return model;
    }

    public static AbstractModel Get(Type type)
    {
        int gen = Volatile.Read(ref _generation);
        if (ModelsByType.TryGetValue(type, out (int Gen, AbstractModel Model) entry) && entry.Gen == gen)
        {
            return entry.Model;
        }

        AbstractModel model = _contentById[GetId(type)];
        ModelsByType[type] = (gen, model);
        return model;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Character<T>() where T : CharacterModel => Get<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Card<T>() where T : Models.CardModel => Get<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Affliction<T>() where T : AfflictionModel => Get<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Power<T>() where T : Models.PowerModel => Get<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Monster<T>() where T : Models.MonsterModel => Get<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Relic<T>() where T : RelicModel => Get<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Potion<T>() where T : PotionModel => Get<T>();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Orb<T>() where T : OrbModel => Get<T>();

    /// <summary>The four native ModelDb orb-pool entries; Glass is valid for random generation only.</summary>
    public static IEnumerable<OrbModel> Orbs
    {
        get
        {
            int gen = Volatile.Read(ref _generation);
            if (Volatile.Read(ref _orbsGeneration) == gen && _orbsCache is { } cached)
            {
                return cached;
            }

            ReadOnlyCollection<OrbModel> resolved = Array.AsReadOnly(new OrbModel[]
            {
                Orb<LightningOrb>(),
                Orb<FrostOrb>(),
                Orb<DarkOrb>(),
                Orb<PlasmaOrb>(),
            });
            _orbsCache = resolved;
            Volatile.Write(ref _orbsGeneration, gen);
            return resolved;
        }
    }

    private static readonly string[] NativeCharacterOrder =
        ["Ironclad", "Silent", "Regent", "Necrobinder", "Defect", "Fool"];

    /// <summary>Native character order across all ported playable characters.</summary>
    public static IEnumerable<CharacterModel> AllCharacters
    {
        get
        {
            int gen = Volatile.Read(ref _generation);
            if (Volatile.Read(ref _charactersGeneration) == gen && _charactersCache is { } cached)
            {
                return cached;
            }

            ReadOnlyCollection<CharacterModel> resolved = Array.AsReadOnly(_contentById.Values
                .OfType<CharacterModel>()
                .OrderBy(character => Array.IndexOf(NativeCharacterOrder, character.GetType().Name) is int index && index >= 0
                    ? index
                    : int.MaxValue)
                .ThenBy(character => character.GetType().FullName, StringComparer.Ordinal)
                .ToArray());
            _charactersCache = resolved;
            Volatile.Write(ref _charactersGeneration, gen);
            return resolved;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Event<T>() where T : EventModel => Get<T>();

    /// <summary>Returns all registered models of the requested type. 偏离 #68：直接对已注册模型做
    /// 扁平类型过滤，不像真实游戏按 <c>RelicPoolModel</c>/<c>CardPoolModel</c> 分池维护索引——本计划
    /// 内容规模小（个位数到几十个），扁平查询足够快；Plan06b 批量铺开后如证明是性能热点再引入分池索引。</summary>
    public static IEnumerable<T> All<T>() where T : AbstractModel
    {
        int gen = Volatile.Read(ref _generation);
        if (Volatile.Read(ref AllCache<T>.Generation) == gen && AllCache<T>.Items is { } cached)
        {
            return cached;
        }

        T[] items = typeof(T) == typeof(CharacterModel)
            ? AllCharacters.Cast<T>().ToArray()
            : _contentById.Values.OfType<T>().ToArray();
        ReadOnlyCollection<T> resolved = Array.AsReadOnly(items);
        AllCache<T>.Items = resolved;
        Volatile.Write(ref AllCache<T>.Generation, gen);
        return resolved;
    }

    internal static IReadOnlyList<TModel> ResolveRegisteredPool<TModel>(
        Type[] types,
        ref int cachedGeneration,
        ref ReadOnlyCollection<TModel>? cachedModels)
        where TModel : AbstractModel
    {
        int gen = Volatile.Read(ref _generation);
        if (Volatile.Read(ref cachedGeneration) == gen && cachedModels is { } existing)
        {
            return existing;
        }

        var list = new List<TModel>(types.Length);
        for (int i = 0; i < types.Length; i++)
        {
            if (_contentById.TryGetValue(GetId(types[i]), out AbstractModel? model))
            {
                list.Add((TModel)model);
            }
        }

        ReadOnlyCollection<TModel> resolved = Array.AsReadOnly(list.ToArray());
        cachedModels = resolved;
        Volatile.Write(ref cachedGeneration, gen);
        return resolved;
    }

    internal static IReadOnlyList<TModel> ResolveStrictPool<TModel>(
        Type[] types,
        ref int cachedGeneration,
        ref ReadOnlyCollection<TModel>? cachedModels)
        where TModel : AbstractModel
    {
        int gen = Volatile.Read(ref _generation);
        if (Volatile.Read(ref cachedGeneration) == gen && cachedModels is { } existing)
        {
            return existing;
        }

        var items = new TModel[types.Length];
        for (int i = 0; i < types.Length; i++)
        {
            items[i] = (TModel)_contentById[GetId(types[i])];
        }

        ReadOnlyCollection<TModel> resolved = Array.AsReadOnly(items);
        cachedModels = resolved;
        Volatile.Write(ref cachedGeneration, gen);
        return resolved;
    }
}
