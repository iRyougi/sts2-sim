using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;

namespace Sts2Sim.Core.Content.Acts;

public static partial class UnderdocksEncounters
{
    public static IReadOnlyList<EncounterDefinition> BatchA { get; } =
    [
        new(CreateCorpseSlugsNormal, [EncounterTag.Slugs], false, "CorpseSlugsNormal"),
        new(CreateCultistsNormal, null, false, "CultistsNormal"),
        new(() => [(CloneMonster<FossilStalker>(), null)], null, false, "FossilStalkerNormal"),
        new(() => [(CloneMonster<GremlinMerc>(), "merc")], null, false, "GremlinMercNormal")
        {
            GoldProportionCalculator = combatState =>
                !combatState.EscapedCreatures.Any(creature => creature.Monster is FatGremlin)
                    ? 1f
                    : combatState.GoldWasStolen ? 0f : 0.5f,
        },
        new(() => [(CloneMonster<HauntedShip>(), null)], null, false, "HauntedShipNormal"),
        new(() => [(CloneMonster<LivingFog>(), "livingFog")], null, false, "LivingFogNormal")
        {
            Slots = ["bomb1", "bomb2", "bomb3", "bomb4", "bomb5", "livingFog"],
        },
        new(() => [(CloneMonster<PunchConstruct>(), null)], null, false, "PunchConstructNormal"),
    ];

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateCorpseSlugsNormal(Sts2Sim.Core.Random.Rng rng)
    {
        CorpseSlug[] slugs = [CloneMonster<CorpseSlug>(), CloneMonster<CorpseSlug>(), CloneMonster<CorpseSlug>()];
        CorpseSlug.EnsureCorpseSlugsStartWithDifferentMoves(slugs, rng);
        return slugs.Select(slug => ((MonsterModel)slug, (string?)null)).ToArray();
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateCultistsNormal() =>
    [
        (CloneMonster<CalcifiedCultist>(), null),
        (CloneMonster<DampCultist>(), null),
    ];

    private static T CloneMonster<T>() where T : MonsterModel =>
        (T)ModelDb.Monster<T>().MutableClone();
}
