using System.Diagnostics.CodeAnalysis;
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
    }

    /// <summary>仅供测试清场使用(ModelDb 在生产路径上 Init 后只读)。</summary>
    public static void ResetForTests()
    {
        _contentById.Clear();
    }

    public static ModelId GetId<T>() where T : AbstractModel
    {
        return GetId(typeof(T));
    }

    public static ModelId GetId(Type type)
    {
        return new ModelId(GetCategory(type), GetEntry(type));
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

    private static T Get<T>() where T : AbstractModel
    {
        return (T)Get(typeof(T));
    }

    public static AbstractModel Get(Type type) => _contentById[GetId(type)];

    public static T Character<T>() where T : CharacterModel
    {
        return Get<T>();
    }

    public static T Card<T>() where T : Models.CardModel => Get<T>();

    public static T Affliction<T>() where T : AfflictionModel => Get<T>();

    public static T Power<T>() where T : Models.PowerModel => Get<T>();

    public static T Monster<T>() where T : Models.MonsterModel => Get<T>();

    public static T Relic<T>() where T : RelicModel => Get<T>();

    public static T Potion<T>() where T : PotionModel => Get<T>();

    public static T Orb<T>() where T : OrbModel => Get<T>();

    /// <summary>The four native ModelDb orb-pool entries; Glass is valid for random generation only.</summary>
    public static IEnumerable<OrbModel> Orbs =>
    [
        Orb<LightningOrb>(),
        Orb<FrostOrb>(),
        Orb<DarkOrb>(),
        Orb<PlasmaOrb>(),
    ];

    /// <summary>Native character order, omitting characters whose content has not been ported.</summary>
    public static IEnumerable<CharacterModel> AllCharacters => _contentById.Values
        .OfType<CharacterModel>()
        .OrderBy(character => Array.IndexOf(
            ["Ironclad", "Silent", "Regent", "Necrobinder", "Defect"],
            character.GetType().Name) is int index && index >= 0 ? index : int.MaxValue)
        .ThenBy(character => character.GetType().FullName, StringComparer.Ordinal);

    public static T Event<T>() where T : EventModel => Get<T>();

    /// <summary>Returns all registered models of the requested type. 偏离 #68：直接对已注册模型做
    /// 扁平类型过滤，不像真实游戏按 <c>RelicPoolModel</c>/<c>CardPoolModel</c> 分池维护索引——本计划
    /// 内容规模小（个位数到几十个），扁平查询足够快；Plan06b 批量铺开后如证明是性能热点再引入分池索引。</summary>
    public static IEnumerable<T> All<T>() where T : AbstractModel =>
        typeof(T) == typeof(CharacterModel)
            ? AllCharacters.Cast<T>()
            : _contentById.Values.OfType<T>();
}
