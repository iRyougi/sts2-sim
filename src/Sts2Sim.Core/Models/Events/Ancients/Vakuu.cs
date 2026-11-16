using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class Vakuu : AncientEventModel
{
    private static readonly Type[] Pool1 = [typeof(BloodSoakedRose), typeof(WhisperingEarring), typeof(Fiddle)];
    private static readonly Type[] Pool2 = [typeof(PreservedFog), typeof(SereTalon), typeof(DistinguishedCape)];
    private static readonly Type[] Pool3 = [typeof(ChoicesParadox), typeof(MusicBox), typeof(LordsParasol), typeof(JeweledMask)];

    public override IReadOnlyList<RelicModel> AllPossibleOptions => Pool1.Concat(Pool2).Concat(Pool3)
        .Select(type => (RelicModel)ModelDb.Get(type).MutableClone()).ToArray();

    protected override VisibleDecisionMetadata BuildVisibleState() =>
        base.BuildVisibleState() with { CoverageGapIds = ["G3.VakuuVisits"] };

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        // v0.113.1 GetOptionsFromThreePools：每池先按 relic.IsAllowed 过滤再各 Rng.NextItem 一次。
        // 这三池在标准单人下无不可用遗物（账本"模拟器现状已一致"·AncientEventModel 组），过滤恒等、回退补抽不触发。
        Type first = Rng.NextItem(Pool1)!;
        Type second = Rng.NextItem(Pool2)!;
        Type third = Rng.NextItem(Pool3)!;
        return [RelicOption(first, first.Name), RelicOption(second, second.Name), RelicOption(third, third.Name)];
    }
}
