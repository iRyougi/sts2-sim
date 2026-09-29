namespace Sts2Sim.Core.Runs;

/// <summary>一局跑局的终止原因。
///
/// 此前只能从 <c>Won</c> / <c>Survived</c> / <c>Truncated</c> 三个布尔间接推断，
/// 其中 <c>Truncated</c> 是 <c>!exhaustedMap</c> 算出来的——"地图耗尽"和"达到层数上限"
/// 因此无法区分，而这两者一个是引擎缺陷、一个是正常收束。
///
/// 偏离 #320（自由旅行走到最后一行被误判地图耗尽）当时只能靠人工比对
/// "156 条路径 + 满血 + Won=false"才认出来；有了本枚举，那一类可以机器判定。</summary>
public enum RunOutcome
{
    /// <summary>打通最终幕 Boss。</summary>
    Victory,

    /// <summary>玩家在跑局过程中死亡（<c>RunState.IsGameOver</c>）。</summary>
    PlayerDefeated,

    /// <summary>取点入口返回空集合——当前点没有任何可前往的地图点。
    ///
    /// **这不一定是合法终止。** 参见 <c>MapExhaustedWithReachableChildren</c>：
    /// 若终止时当前点其实还有 <c>Children</c>，那就是取点逻辑的缺陷而非真的走到头。</summary>
    MapExhausted,

    /// <summary>达到 <c>maxFloors</c> 上限而收束，既没死也没通关。</summary>
    FloorLimitReached,

    /// <summary><c>RunAsync</c> 进入时局面已经结束（含起点先古之民房内死亡）。</summary>
    AlreadyOver,
}
