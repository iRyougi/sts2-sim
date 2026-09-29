using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Events;

public sealed class Vakuu : AncientEventModel
{
    private static readonly Type[] Pool1 = [typeof(BloodSoakedRose), typeof(WhisperingEarring), typeof(Fiddle)];
    private static readonly Type[] Pool2 = [typeof(PreservedFog), typeof(SereTalon), typeof(DistinguishedCape)];
    private static readonly Type[] Pool3 = [typeof(ChoicesParadox), typeof(MusicBox), typeof(LordsParasol), typeof(JeweledMask)];

    public override IReadOnlyList<RelicModel> AllPossibleOptions => Pool1.Concat(Pool2).Concat(Pool3)
        .Select(type => (RelicModel)ModelDb.Get(type).MutableClone()).ToArray();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        List<Type> first = Pool1.ToList();
        List<Type> second = Pool2.ToList();
        List<Type> third = Pool3.ToList();
        first.UnstableShuffle(Rng);
        second.UnstableShuffle(Rng);
        third.UnstableShuffle(Rng);
        return [RelicOption(first[0], first[0].Name), RelicOption(second[0], second[0].Name), RelicOption(third[0], third[0].Name)];
    }
}
