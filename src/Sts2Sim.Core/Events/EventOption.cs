namespace Sts2Sim.Core.Events;

/// <summary>A selectable event option identified by its event-local key. 偏离 #82：省略真实游戏里
/// UI/本地化/多人相关部分（<c>Title</c>/<c>HoverTips</c>/<c>Description</c>/<c>DialogueSet</c>/
/// <c>IsShared</c>），只保留状态机骨架——无头模拟器没有渲染/本地化层，<see cref="Key"/> 字段够决策层
/// 引用即可。</summary>
public sealed class EventOption
{
    private readonly Func<Task>? _onChosen;

    public string Key { get; }

    public bool IsLocked => _onChosen is null;

    public bool IsEnabled => _onChosen is not null;

    public EventOption(string key, Func<Task>? onChosen)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        _onChosen = onChosen;
    }

    internal EventOption RebindForRun(Func<Delegate, Delegate> mapCallback)
    {
        // One graph mapper owns all Method/Target and invocation-list reconstruction.
        Func<Task>? rebound = _onChosen is null
            ? null : (Func<Task>)mapCallback(_onChosen);
        return new EventOption(Key, rebound);
    }

    public Task Invoke() => _onChosen?.Invoke()
        ?? throw new InvalidOperationException("Locked event options cannot be chosen.");
}
