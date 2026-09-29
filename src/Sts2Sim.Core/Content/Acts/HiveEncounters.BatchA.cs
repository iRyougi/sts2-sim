namespace Sts2Sim.Core.Content.Acts;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;

public static partial class HiveEncounters
{
    public static IReadOnlyList<EncounterDefinition> BatchA { get; } =
    [
        new(CreateBowlbugsNormal, [EncounterTag.Workers], false, "BowlbugsNormal"),
        new(CreateBowlbugsWeak, [EncounterTag.Workers], true, "BowlbugsWeak"),
        new(CreateDecimillipede, null, false, "DecimillipedeElite"),
        new(() => [(Monster<Entomancer>(), null)], null, false, "EntomancerElite"),
        new(() => CreateExoskeletons(4), [EncounterTag.Exoskeletons], false, "ExoskeletonsNormal"),
        new(() => CreateExoskeletons(3), [EncounterTag.Exoskeletons], true, "ExoskeletonsWeak"),
        new(() => [(Monster<HunterKiller>(), null)], null, false, "HunterKillerNormal"),
        new(() => [(Monster<InfestedPrism>(), null)], null, false, "InfestedPrismsElite"),
        new(() => [(Monster<Ovicopter>(), "ovicopter")], null, false, "OvicopterNormal"),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateBowlbugsNormal(Rng rng)
    {
        var workers = new List<MonsterModel>
        {
            Monster<BowlbugEgg>(),
            Monster<BowlbugSilk>(),
            Monster<BowlbugNectar>(),
        };
        MonsterModel first = rng.NextItem(workers)!;
        workers.Remove(first);
        MonsterModel second = rng.NextItem(workers)!;
        return
        [
            (Monster<BowlbugRock>(), "first"),
            (first, "middle"),
            (second, "last"),
        ];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateBowlbugsWeak(Rng rng) =>
    [
        (Monster<BowlbugRock>(), "odd"),
        (rng.NextBool() ? Monster<BowlbugEgg>() : Monster<BowlbugNectar>(), "even"),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateDecimillipede(Rng rng)
    {
        int start = rng.NextInt(3);
        var front = MutableMonster<DecimillipedeSegmentFront>();
        var middle = MutableMonster<DecimillipedeSegmentMiddle>();
        var back = MutableMonster<DecimillipedeSegmentBack>();
        front.StarterMoveIdx = start;
        middle.StarterMoveIdx = (start + 1) % 3;
        back.StarterMoveIdx = (start + 2) % 3;
        return [(front, "segment1"), (middle, "segment2"), (back, "segment3")];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateExoskeletons(int count)
    {
        string[] slots = ["first", "second", "third", "fourth"];
        return Enumerable.Range(0, count)
            .Select(index => ((MonsterModel)Monster<Exoskeleton>(), (string?)slots[index]))
            .ToArray();
    }

    private static T Monster<T>() where T : MonsterModel => ModelDb.Monster<T>();

    private static T MutableMonster<T>() where T : MonsterModel =>
        (T)ModelDb.Monster<T>().MutableClone();
}
