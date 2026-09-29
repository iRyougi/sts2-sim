using System.Reflection;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Models;

/// <summary>
/// MutableClone 深拷贝契约的守护测试：canonical 模板与克隆体之间不允许共享任何可变引用类型
/// 字段——一旦共享，所有从同一 canonical 派生的战斗实例会静默互相污染状态（canonical 是进程级
/// static 单例，这种泄漏跨战斗、跨 run、跨并行环境实例传播且难以定位）。
/// 通过反射自动覆盖程序集内全部具体 AbstractModel 子类：Plan06 新增的每个卡牌/遗物/怪物/power
/// 都会被本测试自动检查，忘记覆写 DeepCloneFields/AfterCloned 时在这里立刻失败，而不是等到
/// 两场战斗同时存在时才暴露。
/// </summary>
public class MutableCloneIsolationTests : IDisposable
{
    /// <summary>允许 canonical 与克隆体共享引用的不可变类型。新增类型前先确认它真的不可变。</summary>
    private static readonly Type[] _immutableShareableTypes =
    {
        typeof(string),
        typeof(Type),
        typeof(ModelId),
    };

    public MutableCloneIsolationTests()
    {
        ModelDb.ResetForTests();
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<Type> AllConcreteModelTypes()
    {
        var data = new TheoryData<Type>();
        foreach (Type type in typeof(AbstractModel).Assembly.GetTypes())
        {
            if (typeof(AbstractModel).IsAssignableFrom(type) && !type.IsAbstract)
            {
                data.Add(type);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllConcreteModelTypes))]
    public void MutableClone_SharesNoMutableReferenceFieldsWithCanonical(Type modelType)
    {
        ModelDb.Inject(modelType);
        var canonical = ModelDb.GetById<AbstractModel>(ModelDb.GetId(modelType));
        AbstractModel clone = canonical.MutableClone();

        var sharedFields = new List<string>();
        for (Type? type = modelType; type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.FieldType.IsValueType || field.FieldType.IsPointer)
                {
                    continue;
                }

                object? canonicalValue = field.GetValue(canonical);
                object? cloneValue = field.GetValue(clone);
                if (canonicalValue is null || !ReferenceEquals(canonicalValue, cloneValue))
                {
                    continue;
                }

                if (_immutableShareableTypes.Contains(canonicalValue.GetType()) ||
                    canonicalValue is Type)
                {
                    continue;
                }

                sharedFields.Add($"{type.Name}.{field.Name} ({field.FieldType.Name})");
            }
        }

        Assert.True(
            sharedFields.Count == 0,
            $"{modelType.Name} 的克隆体与 canonical 共享了可变引用字段（缺少 DeepCloneFields/AfterCloned " +
            $"处理）：{string.Join(", ", sharedFields)}");
    }
}
