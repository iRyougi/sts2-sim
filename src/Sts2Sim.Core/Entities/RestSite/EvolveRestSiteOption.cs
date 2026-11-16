using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.RestSite;

/// <summary>v0.113.1 <c>EvolveRestSiteOption</c>（EVOLVE）：把 Byrdpip 替换成 Byrdine（继承皮肤 → 发型）。
/// 原版选择后只播放进化动画（<c>NByrdEvolutionVfx</c>，无玩家选择）并暂时禁用地图/前进按钮，偏离 #160 省略。
/// 原版找不到 Byrdpip 时返回 false（选项未成功）；模拟器在不可达的这一分支显式失败。</summary>
public sealed record EvolveRestSiteOption : RestSiteDecision
{
    public override string OptionId => "evolve";
    public override int Priority => 4;

    public override async Task ExecuteAsync(Player player)
    {
        Byrdpip byrdpip = player.Relics.OfType<Byrdpip>().FirstOrDefault()
            ?? throw new InvalidOperationException("Evolve requires Byrdpip.");
        var byrdine = (Byrdine)ModelDb.Relic<Byrdine>().MutableClone();
        await RelicCmd.Replace(byrdpip, byrdine);
    }
}
