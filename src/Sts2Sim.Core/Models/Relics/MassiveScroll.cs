using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class MassiveScroll : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    /// <summary>偏离 #151：多人专属遗物在本项目的单人运行中恒不允许，并保留安全 no-op 拾取实现。</summary>
    public override bool IsAllowed(IRunState runState) => false;

    public override Task AfterObtained() => Task.CompletedTask;
}
