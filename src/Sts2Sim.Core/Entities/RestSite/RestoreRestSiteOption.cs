using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.RestSite;

/// <summary>v0.113.1 <c>RestoreRestSiteOption</c>（RESTORE）：持有 Wuppy 且 Wuppy.CurrentHp &lt; MaxHp 时可选，
/// 选择后把所有 Wuppy 回满。<paramref name="Enabled"/> 在营火快照生成时取原版 <c>IsEnabled</c> 的值：营火内没有
/// 其他动作会改变 Wuppy 的保存 HP，快照值与原版实时求值一致；执行时仍按实时状态重新校验。</summary>
public sealed record RestoreRestSiteOption(bool Enabled) : RestSiteDecision
{
    public override string OptionId => "restore";
    public override bool IsEnabled => Enabled;
    public override int Priority => 4;

    public override Task ExecuteAsync(Player player)
    {
        Wuppy? first = player.Relics.OfType<Wuppy>().FirstOrDefault();
        if (first is null || first.CurrentHp >= first.MaxHp)
        {
            throw new InvalidOperationException("Restore requires a Wuppy below its maximum HP.");
        }
        foreach (Wuppy wuppy in player.Relics.OfType<Wuppy>())
        {
            wuppy.CurrentHp = wuppy.MaxHp;
        }
        return Task.CompletedTask;
    }
}
