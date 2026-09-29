namespace Sts2Sim.Core.Models.Exceptions;

/// <summary>类型已注册进 ModelDb 后又被直接 new——应从 ModelDb 取 canonical 实例再 MutableClone。</summary>
public sealed class DuplicateModelException(Type type)
    : InvalidOperationException($"Model type {type.Name} is already registered in ModelDb; get the canonical instance and clone it instead.");

/// <summary>对 canonical(不可变)模型执行了要求可变实例的操作。</summary>
public sealed class CanonicalModelException(Type type)
    : InvalidOperationException($"Model {type.Name} is canonical (immutable); call MutableClone() first.");

/// <summary>对可变模型执行了要求 canonical 实例的操作。</summary>
public sealed class MutableModelException(Type type)
    : InvalidOperationException($"Model {type.Name} is mutable; this operation requires the canonical instance.");

/// <summary>ModelDb 中不存在该 id。</summary>
public sealed class ModelNotFoundException(ModelId id)
    : InvalidOperationException($"No model registered for id {id}.");
