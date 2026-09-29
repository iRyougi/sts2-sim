using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Runs;

[Collection("ModelDb")]
public sealed class RelicPoolGatingTests : IDisposable
{
    public RelicPoolGatingTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Silent_CannotRollRegentExclusiveRelics()
    {
        Player player = CreatePlayer<Silent>();

        ISet<Type> rolledTypes = PullAllRewardRelics(player);

        Assert.DoesNotContain(typeof(DivineRight), rolledTypes);
        Assert.DoesNotContain(typeof(FencingManual), rolledTypes);
        Assert.DoesNotContain(typeof(GalacticDust), rolledTypes);
        Assert.DoesNotContain(typeof(LunarPastry), rolledTypes);
        Assert.DoesNotContain(typeof(MiniRegent), rolledTypes);
        Assert.DoesNotContain(typeof(OrangeDough), rolledTypes);
        Assert.DoesNotContain(typeof(Regalite), rolledTypes);
        Assert.DoesNotContain(typeof(VitruvianMinion), rolledTypes);
    }

    [Fact]
    public void Regent_CannotRollSilentExclusiveRelics()
    {
        Player player = CreatePlayer<Regent>();

        ISet<Type> rolledTypes = PullAllRewardRelics(player);

        Assert.DoesNotContain(typeof(HelicalDart), rolledTypes);
        Assert.DoesNotContain(typeof(NinjaScroll), rolledTypes);
        Assert.DoesNotContain(typeof(PaperKrane), rolledTypes);
        Assert.DoesNotContain(typeof(RingOfTheSnake), rolledTypes);
        Assert.DoesNotContain(typeof(SneckoSkull), rolledTypes);
        Assert.DoesNotContain(typeof(Tingsha), rolledTypes);
        Assert.DoesNotContain(typeof(ToughBandages), rolledTypes);
        Assert.DoesNotContain(typeof(TwistedFunnel), rolledTypes);
    }

    [Fact]
    public void Silent_CanRollOwnExclusiveRelics()
    {
        IReadOnlyList<RelicModel> pool = ModelDb.Character<Silent>().RelicPool.AllRelics;

        Assert.Contains(pool, relic => relic is HelicalDart);
        Assert.Contains(pool, relic => relic is NinjaScroll);
        Assert.Contains(pool, relic => relic is PaperKrane);
        Assert.Contains(pool, relic => relic is RingOfTheSnake);
        Assert.Contains(pool, relic => relic is SneckoSkull);
        Assert.Contains(pool, relic => relic is Tingsha);
        Assert.Contains(pool, relic => relic is ToughBandages);
        Assert.Contains(pool, relic => relic is TwistedFunnel);
    }

    [Fact]
    public void Silent_RewardBagContainsEveryRewardableExclusiveRelicInCorrectBucket()
    {
        IReadOnlyDictionary<RelicRarity, ISet<Type>> rolledTypes = PullRewardRelicsByRarity(CreatePlayer<Silent>());

        AssertCharacterExclusiveRewardBuckets(
            rolledTypes,
            common: new[] { typeof(SneckoSkull) },
            uncommon: new[] { typeof(Tingsha), typeof(TwistedFunnel) },
            rare: new[] { typeof(HelicalDart), typeof(PaperKrane), typeof(ToughBandages) },
            shop: new[] { typeof(NinjaScroll) });
    }

    [Fact]
    public void Regent_RewardBagContainsEveryRewardableExclusiveRelicInCorrectBucket()
    {
        IReadOnlyDictionary<RelicRarity, ISet<Type>> rolledTypes = PullRewardRelicsByRarity(CreatePlayer<Regent>());

        AssertCharacterExclusiveRewardBuckets(
            rolledTypes,
            common: new[] { typeof(FencingManual) },
            uncommon: new[] { typeof(GalacticDust), typeof(Regalite) },
            rare: new[] { typeof(LunarPastry), typeof(MiniRegent), typeof(OrangeDough) },
            shop: new[] { typeof(VitruvianMinion) });
    }

    [Fact]
    public void BothCharacters_ShareTheSharedPool()
    {
        ISet<Type> silentRolls = PullAllRewardRelics(CreatePlayer<Silent>());
        ISet<Type> regentRolls = PullAllRewardRelics(CreatePlayer<Regent>());

        Assert.Contains(typeof(Akabeko), silentRolls);
        Assert.Contains(typeof(Akabeko), regentRolls);
    }

    [Fact]
    public void SilentPool_ExcludesStartingRelicFromRewards()
    {
        ISet<Type> rolledTypes = PullAllRewardRelics(CreatePlayer<Silent>());

        Assert.DoesNotContain(typeof(RingOfTheSnake), rolledTypes);
    }

    private static Player CreatePlayer<TCharacter>() where TCharacter : CharacterModel =>
        Player.CreateForNewRun(
            ModelDb.Character<TCharacter>(),
            new RunState($"relic-pool-{typeof(TCharacter).Name}", new Overgrowth()));

    private static ISet<Type> PullAllRewardRelics(Player player)
    {
        return PullRewardRelicsByRarity(player)
            .Values
            .SelectMany(types => types)
            .ToHashSet();
    }

    private static IReadOnlyDictionary<RelicRarity, ISet<Type>> PullRewardRelicsByRarity(Player player)
    {
        var result = new Dictionary<RelicRarity, ISet<Type>>();
        foreach (RelicRarity rarity in new[]
                 {
                     RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop,
                 })
        {
            var bucket = new HashSet<Type>();
            RelicModel? relic;
            while ((relic = player.RelicGrabBag.PullFromFront(rarity)) is not null)
            {
                bucket.Add(relic.GetType());
            }

            result.Add(rarity, bucket);
        }

        return result;
    }

    private static void AssertCharacterExclusiveRewardBuckets(
        IReadOnlyDictionary<RelicRarity, ISet<Type>> rolledTypes,
        IReadOnlyCollection<Type> common,
        IReadOnlyCollection<Type> uncommon,
        IReadOnlyCollection<Type> rare,
        IReadOnlyCollection<Type> shop)
    {
        AssertExclusiveRewardBucket(rolledTypes, RelicRarity.Common, common);
        AssertExclusiveRewardBucket(rolledTypes, RelicRarity.Uncommon, uncommon);
        AssertExclusiveRewardBucket(rolledTypes, RelicRarity.Rare, rare);
        AssertExclusiveRewardBucket(rolledTypes, RelicRarity.Shop, shop);
    }

    private static void AssertExclusiveRewardBucket(
        IReadOnlyDictionary<RelicRarity, ISet<Type>> rolledTypes,
        RelicRarity rarity,
        IReadOnlyCollection<Type> expectedTypes)
    {
        IEnumerable<Type> actualTypes = rolledTypes[rarity]
            .Where(RewardableCharacterExclusiveTypes.Contains)
            .OrderBy(type => type.Name);

        Assert.Equal(expectedTypes.OrderBy(type => type.Name), actualTypes);
    }

    private static readonly Type[] RewardableCharacterExclusiveTypes =
    {
        typeof(FencingManual), typeof(GalacticDust), typeof(LunarPastry), typeof(MiniRegent),
        typeof(OrangeDough), typeof(Regalite), typeof(VitruvianMinion), typeof(HelicalDart),
        typeof(NinjaScroll), typeof(PaperKrane), typeof(SneckoSkull), typeof(Tingsha),
        typeof(ToughBandages), typeof(TwistedFunnel),
    };
}
