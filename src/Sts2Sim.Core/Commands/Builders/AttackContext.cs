using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Commands.Builders;

/// <summary>
/// Groups direct damage calls into one authoritative attack hook boundary.
/// </summary>
public sealed class AttackContext : IAsyncDisposable
{
    private readonly ICombatState _combatState;
    private readonly AttackCommand _attackCommand;
    private bool _disposed;

    private AttackContext(ICombatState combatState, CardPlay cardPlay)
    {
        _combatState = combatState;
        _attackCommand = new AttackCommand(0m)
            .FromCard(cardPlay.Card, cardPlay)
            .TargetingAllOpponents(combatState);
    }

    internal static async Task<AttackContext> CreateAsync(ICombatState combatState, CardPlay cardPlay)
    {
        var context = new AttackContext(combatState, cardPlay);
        await Hook.BeforeAttack(combatState, context._attackCommand);
        return context;
    }

    public void AddHit(IEnumerable<DamageResult> results)
    {
        _attackCommand.IncrementHitsInternal();
        _attackCommand.AddResultsInternal(results);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await Hook.AfterAttack(_combatState, _attackCommand);
        }
        catch (Exception exception) when (exception is not DecisionSuspendedException)
        {
            // 偏离：权威实现通过 Log.Error 记录此异常；Core 当前没有日志抽象或日志依赖，
            // 因此先保持权威异常隔离语义并最小吞并，待仓库统一引入 logger 后再接入记录。
        }
    }
}
