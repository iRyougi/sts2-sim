namespace Sts2Sim.Core.Content.Acts;

using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;

public static partial class GloryEncounters
{
    public static IReadOnlyList<EncounterDefinition> BatchB { get; } =
    [
        new(
            () =>
            [
                (Monster<PunchConstruct>(), null),
                (Monster<CubexConstruct>(), null),
                (Monster<CubexConstruct>(), null),
            ],
            null,
            false,
            "ConstructMenagerieNormal"),
        new(
            () =>
            [
                (Monster<FlailKnight>(), "first"),
                (Monster<SpectralKnight>(), "second"),
                (Monster<MagiKnight>(), "third"),
            ],
            [EncounterTag.Knights],
            false,
            "KnightsElite"),
        new(CreateScrollsNormal, [EncounterTag.Scrolls], false, "ScrollsOfBitingNormal"),
        new(CreateScrollsWeak, [EncounterTag.Scrolls], true, "ScrollsOfBitingWeak"),
        new(() => [(Monster<SlimedBerserker>(), null)], null, false, "SlimedBerserkerNormal"),
        new(() => [(Monster<SoulNexus>(), null)], null, false, "SoulNexusElite"),
        new(() => [(Monster<TheLost>(), null), (Monster<TheForgotten>(), null)], null, false, "TheLostAndForgottenNormal"),
        new(() => [(Monster<LivingShield>(), null), (Monster<TurretOperator>(), null)], null, true, "TurretOperatorWeak"),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateScrollsNormal(Rng rng) => CreateScrolls(rng, 4);
    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateScrollsWeak(Rng rng) => CreateScrolls(rng, 3);

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateScrolls(Rng rng, int count)
    {
        int start = rng.NextInt(3);
        var result = new List<(MonsterModel Monster, string? SlotName)>();
        for (int index = 0; index < count; index++)
        {
            var scroll = (ScrollOfBiting)Monster<ScrollOfBiting>().MutableClone();
            scroll.StarterMoveIdx = index == 3 ? 2 : (start + index) % 3;
            result.Add((scroll, null));
        }

        return result;
    }
}
