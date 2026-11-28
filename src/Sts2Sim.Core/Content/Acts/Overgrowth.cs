using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Dread.Dominions;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Content.Acts;

/// <summary>Act 1 definition, including the complete 22-encounter Overgrowth roster.</summary>
public sealed class Overgrowth : ActDefinition
{
    public override int Index => 0;

    public override IReadOnlyList<Type> EventPool => Act1EventPool.All;

    private static readonly IReadOnlyList<Type> StaticAncientPool = [typeof(Neow)];

    public override IReadOnlyList<Type> AncientPool => StaticAncientPool;

    public override int BaseNumberOfRooms => 15;

    public override int NumberOfWeakEncounters => 3;

    // 下列遭遇的 Name 早于"按原版类名命名"的约定，与原版 ModelId.Entry 不同，IdEntry 显式写出原版 ID。
    // 原版用 Id.Entry 派生怪物生成种子（RoomFactory.CreateEncounterMonsterRng），写错会改变怪物组成。
    private static readonly IReadOnlyList<EncounterDefinition> StaticMonsterEncounters = new List<EncounterDefinition>
    {
        new(CreateMonster<CubexConstruct>, tags: null, isWeak: false, name: "CubexConstruct") { IdEntry = "CUBEX_CONSTRUCT_NORMAL" },
        new(CreateFlyconid, [EncounterTag.Mushroom, EncounterTag.Slimes], isWeak: false, name: "FlyconidNormal"),
        new(() => [(CreateMonster<Fogmog>(), "fogmog")], tags: null, isWeak: false, name: "Fogmog") { Slots = ["illusion", "fogmog"], IdEntry = "FOGMOG_NORMAL" },
        new(CreateMonster<FuzzyWurmCrawler>, [EncounterTag.Crawler], isWeak: true, name: "FuzzyWurmCrawler") { IdEntry = "FUZZY_WURM_CRAWLER_WEAK" },
        new(CreateInklets, tags: null, isWeak: false, name: "Inklets") { IdEntry = "INKLETS_NORMAL" },
        new(CreateMonster<Mawler>, tags: null, isWeak: false, name: "Mawler") { IdEntry = "MAWLER_NORMAL" },
        new(CreateNibbits, tags: null, isWeak: false, name: "Nibbits") { Slots = ["front", "back"], IdEntry = "NIBBITS_NORMAL" },
        new(CreateLoneNibbit, [EncounterTag.Nibbit], isWeak: true, name: "LoneNibbit") { IdEntry = "NIBBITS_WEAK" },
        new(() =>
        [
            (CreateMonster<ShrinkerBeetle>(), null),
            (CreateMonster<FuzzyWurmCrawler>(), null),
        ], [EncounterTag.Shrinker, EncounterTag.Crawler], isWeak: false, name: "ShrinkerBeetleAndFuzzyWurmCrawler") { IdEntry = "OVERGROWTH_CRAWLERS" },
        new(CreateRubyRaiders, tags: null, isWeak: false, name: "RubyRaiders") { IdEntry = "RUBY_RAIDERS_NORMAL" },
        new(CreateMonster<ShrinkerBeetle>, [EncounterTag.Shrinker], isWeak: true, name: "ShrinkerBeetle") { IdEntry = "SHRINKER_BEETLE_WEAK" },
        new(CreateSlimesNormal, [EncounterTag.Slimes], isWeak: false, name: "SlimesNormal"),
        new(CreateSlimesWeak, [EncounterTag.Slimes], isWeak: true, name: "SlimesWeak"),
        new(CreateSlitheringStrangler, [EncounterTag.Jaxfruit, EncounterTag.Slimes], isWeak: false, name: "SlitheringStrangler") { IdEntry = "SLITHERING_STRANGLER_NORMAL" },
        new(() =>
        [
            (CreateMonster<SnappingJaxfruit>(), null),
            (CreateMonster<Flyconid>(), null),
        ], [EncounterTag.Mushroom, EncounterTag.Jaxfruit], isWeak: false, name: "SnappingJaxfruitAndFlyconid") { IdEntry = "SNAPPING_JAXFRUIT_NORMAL" },
        new(CreateMonster<VineShambler>, tags: null, isWeak: false, name: "VineShambler") { IdEntry = "VINE_SHAMBLER_NORMAL" },
    };

    private static readonly IReadOnlyList<EncounterDefinition> StaticEliteEncounters = new List<EncounterDefinition>
    {
        new(CreateMonster<BygoneEffigy>, tags: null, isWeak: false, name: "BygoneEffigy") { IdEntry = "BYGONE_EFFIGY_ELITE" },
        new(CreateMonster<Byrdonis>, tags: null, isWeak: false, name: "Byrdonis") { IdEntry = "BYRDONIS_ELITE" },
        new(() => [(CreateMonster<PhrogParasite>(), "phrog")], tags: null, isWeak: false, name: "PhrogParasite") { Slots = ["phrog", "wriggler1", "wriggler2", "wriggler3", "wriggler4"], IdEntry = "PHROG_PARASITE_ELITE" },
    };

    private static readonly IReadOnlyList<EncounterDefinition> StaticBossEncounters = new List<EncounterDefinition>
    {
        new(CreateMonster<CeremonialBeast>, tags: null, isWeak: false, name: "CeremonialBeast")
        {
            IdEntry = "CEREMONIAL_BEAST_BOSS",
            DominionFactory = () => ModelDb.Dominion<CeremonialBeastDominion>(),
        },
        EncounterDefinition.CreateDreadAware(
            CreateTheKin,
            tags: null,
            isWeak: false,
            name: "TheKin",
            idEntry: "THE_KIN_BOSS",
            slotsFactory: isDread => isDread
                ? ["slot1", "slot2", "slot3", "leaderSlot"]
                : ["slot1", "slot2", "leaderSlot"],
            dominionFactory: () => ModelDb.Dominion<KinDominion>()),
        new(CreateMonster<Vantom>, tags: null, isWeak: false, name: "Vantom")
        {
            IdEntry = "VANTOM_BOSS",
            DominionFactory = () => ModelDb.Dominion<VantomDominion>(),
        },
    };

    protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => StaticMonsterEncounters;

    protected override IReadOnlyList<EncounterDefinition> EliteEncounters => StaticEliteEncounters;

    protected override IReadOnlyList<EncounterDefinition> BossEncounters => StaticBossEncounters;

    public override MapPointTypeCounts GetMapPointTypes(Rng mapRng, AscensionManager ascension)
    {
        int restCount = mapRng.NextGaussianInt(7, 1, 6, 7);
        int unknownCount = MapPointTypeCounts.StandardRandomUnknownCount(mapRng);
        return new MapPointTypeCounts(unknownCount, restCount, ascension);
    }

    private static T CreateMonster<T>() where T : MonsterModel, new()
    {
        return ModelDb.Contains(typeof(T)) ? ModelDb.Monster<T>() : new T();
    }

    private static T CreateMutableMonster<T>() where T : MonsterModel, new()
    {
        return (T)CreateMonster<T>().MutableClone();
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateFlyconid(Rng rng)
    {
        MonsterModel[] mediumSlimes = [CreateMonster<LeafSlimeM>(), CreateMonster<TwigSlimeM>()];
        return [(rng.NextItem(mediumSlimes)!, null), (CreateMonster<Flyconid>(), null)];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateInklets()
    {
        var middle = CreateMutableMonster<Inklet>();
        middle.MiddleInklet = true;
        return [(CreateMonster<Inklet>(), null), (middle, null), (CreateMonster<Inklet>(), null)];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateNibbits()
    {
        var front = CreateMutableMonster<Nibbit>();
        front.IsFront = true;
        return [(front, "front"), (CreateMonster<Nibbit>(), "back")];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateLoneNibbit()
    {
        var nibbit = CreateMutableMonster<Nibbit>();
        nibbit.IsAlone = true;
        return [(nibbit, null)];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateRubyRaiders(Rng rng)
    {
        var candidates = new List<MonsterModel>
        {
            CreateMonster<AxeRubyRaider>(),
            CreateMonster<AssassinRubyRaider>(),
            CreateMonster<BruteRubyRaider>(),
            CreateMonster<CrossbowRubyRaider>(),
            CreateMonster<TrackerRubyRaider>(),
        };
        var result = new List<(MonsterModel Monster, string? SlotName)>();
        for (int i = 0; i < 3; i++)
        {
            MonsterModel picked = rng.NextItem(candidates)!;
            candidates.Remove(picked);
            result.Add((picked, null));
        }
        return result;
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateSlimesNormal(Rng rng)
    {
        bool leafFirst = rng.NextBool();
        MonsterModel smallA = leafFirst ? CreateMonster<LeafSlimeS>() : CreateMonster<TwigSlimeS>();
        MonsterModel smallB = leafFirst ? CreateMonster<TwigSlimeS>() : CreateMonster<LeafSlimeS>();
        return
        [
            (CreateMonster<TwigSlimeM>(), null),
            (CreateMonster<LeafSlimeM>(), null),
            (smallA, null),
            (smallB, null),
        ];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateSlimesWeak(Rng rng)
    {
        var smallSlimes = new List<MonsterModel> { CreateMonster<LeafSlimeS>(), CreateMonster<TwigSlimeS>() };
        MonsterModel smallA = rng.NextItem(smallSlimes)!;
        smallSlimes.Remove(smallA);
        MonsterModel smallB = rng.NextItem(smallSlimes)!;
        MonsterModel[] mediumSlimes = [CreateMonster<LeafSlimeM>(), CreateMonster<TwigSlimeM>()];
        MonsterModel medium = rng.NextItem(mediumSlimes)!;
        return [(smallA, null), (medium, null), (smallB, null)];
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateSlitheringStrangler(Rng rng)
    {
        var secondaries = new List<MonsterModel>();
        switch (rng.NextItem(Enum.GetValues<SecondaryEnemyType>()))
        {
            case SecondaryEnemyType.SnappingJaxfruit:
                secondaries.Add(CreateMonster<SnappingJaxfruit>());
                break;
            case SecondaryEnemyType.MediumSlime:
                secondaries.Add(rng.NextItem(
                    new MonsterModel[] { CreateMonster<LeafSlimeM>(), CreateMonster<TwigSlimeM>() })!);
                break;
            case SecondaryEnemyType.SmallSlimes:
                MonsterModel[] smallSlimes = [CreateMonster<LeafSlimeS>(), CreateMonster<TwigSlimeS>()];
                secondaries.Add(rng.NextItem(smallSlimes)!);
                secondaries.Add(rng.NextItem(smallSlimes)!);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        secondaries.Add(CreateMonster<SlitheringStrangler>());
        return secondaries.Select(monster => (monster, (string?)null)).ToArray();
    }

    private static IReadOnlyList<(MonsterModel Monster, string? SlotName)> CreateTheKin(bool isDread)
    {
        var firstFollower = CreateMutableMonster<KinFollower>();
        firstFollower.Index = 0;
        var secondFollower = CreateMutableMonster<KinFollower>();
        secondFollower.Index = 1;
        if (!isDread)
        {
            return
            [
                (firstFollower, "slot1"),
                (secondFollower, "slot2"),
                (CreateMonster<KinPriest>(), "leaderSlot"),
            ];
        }

        var thirdFollower = CreateMutableMonster<KinFollower>();
        thirdFollower.Index = 2;
        return
        [
            (firstFollower, "slot1"),
            (secondFollower, "slot2"),
            (thirdFollower, "slot3"),
            (CreateMonster<KinPriest>(), "leaderSlot"),
        ];
    }

    private enum SecondaryEnemyType
    {
        SnappingJaxfruit,
        MediumSlime,
        SmallSlimes,
    }
}
