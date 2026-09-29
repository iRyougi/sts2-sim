using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Content.Acts;

public static partial class UnderdocksEncounters
{
    public static IReadOnlyList<EncounterDefinition> BatchB { get; } =
    [
        new(CreateSeapunkNormal, [EncounterTag.Seapunk], false, "SeapunkNormal"),
        new(() => [(CloneMonster<SewerClam>(), null)], null, false, "SewerClamNormal"),
        // 原版 TwoTailedRatsNormal.Slots：CallForBackup 召唤后 SortEnemiesBySlotName 按这个顺序重排，
        // 玩家回合开始时敌人按重排后的顺序掷招。
        new(CreateTwoTailedRatsNormal, null, false, "TwoTailedRatsNormal") { Slots = ["first", "second", "third", "fourth", "fifth"] },
        new(CreateCorpseSlugsWeak, [EncounterTag.Slugs], true, "CorpseSlugsWeak"),
        new(() => [(CloneMonster<Seapunk>(), null)], [EncounterTag.Seapunk], true, "SeapunkWeak"),
        new(() => [(CloneMonster<SludgeSpinner>(), null)], null, true, "SludgeSpinnerWeak"),
        new(CreateToadpolesWeak, null, true, "ToadpolesWeak"),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateSeapunkNormal() =>
    [
        (CloneMonster<CalcifiedCultist>(), null),
        (CloneMonster<Seapunk>(), null),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateTwoTailedRatsNormal(Sts2Sim.Core.Random.Rng rng)
    {
        TwoTailedRat[] rats = [CloneMonster<TwoTailedRat>(), CloneMonster<TwoTailedRat>(), CloneMonster<TwoTailedRat>()];
        int starter = rng.NextInt(3);
        for (int index = 0; index < rats.Length; index++)
        {
            rats[index].StarterMoveIndex = (starter + index) % 3;
        }

        return rats.Select((rat, index) =>
            (Monster: (MonsterModel)rat, SlotName: (string?)new[] { "third", "fourth", "fifth" }[index])).ToArray();
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateCorpseSlugsWeak(Sts2Sim.Core.Random.Rng rng)
    {
        CorpseSlug[] slugs = [CloneMonster<CorpseSlug>(), CloneMonster<CorpseSlug>()];
        CorpseSlug.EnsureCorpseSlugsStartWithDifferentMoves(slugs, rng);
        return slugs.Select(slug => ((MonsterModel)slug, (string?)null)).ToArray();
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateToadpolesWeak()
    {
        Toadpole front = CloneMonster<Toadpole>();
        front.IsFront = true;
        Toadpole rear = CloneMonster<Toadpole>();
        rear.IsFront = false;
        return [(front, null), (rear, null)];
    }
}
