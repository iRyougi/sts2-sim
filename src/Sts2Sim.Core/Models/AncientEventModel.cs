using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models;

/// <summary>
/// Ancient event base shared by Neow and the Hive Ancients.
/// Neow resets HP before healing; WearyTraveler applies to that full missing-health amount.
/// 偏离 #144：该钩子对应 WaxChoker 对 Ancient 入口的拦截语义；默认允许，但其他监听器仍可否决。
/// </summary>
public abstract class AncientEventModel : EventModel
{
    /// <summary>本 Ancient 事件理论上可能提供的全部遗物选项（NeowsBones 读取这个属性来排除自身）。</summary>
    public abstract IReadOnlyList<RelicModel> AllPossibleOptions { get; }

    protected EventOption RelicOption<TRelic>(string key)
        where TRelic : RelicModel
        => RelicOption(typeof(TRelic), key);

    /// <summary>非泛型重载：供 <c>Neow</c> 这类需要在运行时按 <see cref="Type"/> 动态选择遗物的调用方使用。</summary>
    protected EventOption RelicOption(Type relicType, string key)
    {
        return new EventOption(key, async () =>
        {
            RelicModel relic = (RelicModel)ModelDb.Get(relicType).MutableClone();
            await RelicCmd.Obtain(relic, Owner);
            Finish();
        });
    }

    /// <summary>Preserve an Ancient option's preconfigured relic state until the player chooses it.</summary>
    protected EventOption RelicOption(RelicModel relic, string key)
    {
        return new EventOption(key, async () =>
        {
            await RelicCmd.Obtain(relic, Owner);
            Finish();
        });
    }

    /// <summary>
    /// 偏离 #144：监听器否决时，仅提供用于完成事件的 PROCEED 选项。
    /// </summary>
    protected sealed override IReadOnlyList<EventOption> GenerateInitialOptionsWrapper()
    {
        if (Hook.ShouldAllowAncient(RunState, Owner, this))
        {
            return base.GenerateInitialOptionsWrapper();
        }

        return new[] { new EventOption("PROCEED", () => { Finish(); return Task.CompletedTask; }) };
    }

    /// <summary>
    /// 偏离 #145：<see cref="EventModel.BeginEvent"/> 目前是同步 API，必须在此同步等待异步治疗命令；
    /// 若将来 BeginEvent 改为异步签名，应同步简化本实现。
    /// </summary>
    protected override void CalculateVars()
    {
        base.CalculateVars();
        // Authoritative BeforeEventStarted resets Neow HP even without the UI animation.
        // At A10 this produces the archived initial heal of 56, rather than retaining 70 HP.
        if (this is Events.Neow)
        {
            Owner.Creature.SetCurrentHpInternal(0m);
        }
        decimal missingHp = Owner.Creature.MaxHp - Owner.Creature.CurrentHp;
        decimal heal = RunState.Ascension.HasLevel(AscensionLevel.WearyTraveler) ? missingHp * 0.8m : missingHp;
        CreatureCmd.Heal(Owner.Creature, heal).GetAwaiter().GetResult();
    }
}
