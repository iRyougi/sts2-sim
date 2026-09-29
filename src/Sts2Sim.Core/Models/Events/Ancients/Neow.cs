using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Models.Events;

/// <summary>Neow Ancient 起始事件。</summary>
public sealed class Neow : AncientEventModel
{
    private static readonly Type[] CurseTypes =
    {
        typeof(CursedPearl),
        typeof(DowsingRod),
        typeof(HeftyTablet),
        typeof(LargeCapsule),
        typeof(LeafyPoultice),
        typeof(NeowsBones),
        typeof(NeowsSacrifice),
        typeof(PrecariousShears),
        typeof(SilkenTress),
        typeof(SilverCrucible),
    };

    private static readonly Type[] PositiveTypes =
    {
        typeof(ArcaneScroll),
        typeof(BoomingConch),
        typeof(FishingRod),
        typeof(GoldenPearl),
        typeof(Kaleidoscope),
        typeof(LeadPaperweight),
        typeof(LostCoffer),
        typeof(MassiveScroll),
        typeof(NeowsTorment),
        typeof(NewLeaf),
        typeof(PhialHolster),
        typeof(PreciseScissors),
        typeof(ScrollBoxes),
        typeof(WingedBoots),
    };

    private static readonly Type[] ExtraPositiveTypes =
    {
        typeof(LavaRock),
        typeof(NeowsTalisman),
        typeof(NutritiousOyster),
        typeof(Pomander),
        typeof(SmallCapsule),
        typeof(StoneHumidifier),
    };

    public override IReadOnlyList<RelicModel> AllPossibleOptions
    {
        get
        {
            // 偏离 #177：延续 AncientEventModel 的模拟器契约，每次返回独立可变克隆，而非 canonical 原型。
            return CurseTypes
                .Concat(PositiveTypes)
                .Concat(ExtraPositiveTypes)
                .Select(type => (RelicModel)ModelDb.Get(type).MutableClone())
                .ToList();
        }
    }

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        // 偏离 #178：模拟器没有 modifier 集合与管线，省略权威实现的 modifier 专属选项分支。
        List<Type> allowedCurses = CurseTypes.Where(IsAllowedAtNeow).ToList();
        Type chosenCurse = Rng.NextItem(allowedCurses)
            ?? throw new InvalidOperationException("No Ancient curse relic is allowed at Neow.");

        var positives = new List<Type>(PositiveTypes);
        RemoveMutuallyExclusivePositives(positives, chosenCurse);

        if (chosenCurse != typeof(LargeCapsule))
        {
            positives.Add(Rng.NextBool() ? typeof(LavaRock) : typeof(SmallCapsule));
        }
        positives.Add(Rng.NextBool() ? typeof(NutritiousOyster) : typeof(StoneHumidifier));
        positives.Add(Rng.NextBool() ? typeof(NeowsTalisman) : typeof(Pomander));

        List<Type> allowedPositives = positives.Where(IsAllowedAtNeow).ToList();
        allowedPositives.UnstableShuffle(Rng);
        Type[] chosenPositives = allowedPositives.Take(2).ToArray();
        if (chosenPositives.Length != 2)
        {
            throw new InvalidOperationException("Fewer than two positive Ancient relics are allowed at Neow.");
        }

        return chosenPositives
            .Select(type => RelicOption(type, type.Name))
            .Append(RelicOption(chosenCurse, chosenCurse.Name))
            .ToArray();
    }

    private static void RemoveMutuallyExclusivePositives(List<Type> positives, Type chosenCurse)
    {
        if (chosenCurse == typeof(CursedPearl))
        {
            positives.Remove(typeof(GoldenPearl));
        }
        else if (chosenCurse == typeof(HeftyTablet))
        {
            positives.Remove(typeof(ArcaneScroll));
        }
        else if (chosenCurse == typeof(LeafyPoultice))
        {
            positives.Remove(typeof(NewLeaf));
        }
        else if (chosenCurse == typeof(PrecariousShears))
        {
            positives.Remove(typeof(PreciseScissors));
        }
        else if (chosenCurse == typeof(NeowsSacrifice))
        {
            positives.Remove(typeof(PhialHolster));
            positives.Remove(typeof(LostCoffer));
        }
    }

    private bool IsAllowedAtNeow(Type relicType)
    {
        var relic = (RelicModel)ModelDb.Get(relicType).MutableClone();
        return relic.IsAllowedAtNeow(RunState);
    }
}
