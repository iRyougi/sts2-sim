using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class ScrollBoxesTests : IDisposable
{
    public ScrollBoxesTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void IsAllowedAtNeow_CountsFromTheCharacterCardPoolNotTheFlatPool()
    {
        // 偏离 #304：权威 CanGenerateBundles 数的是 character.CardPool.GetUnlockedCards(...)。
        // 此前本测试用 ModelDb.Init(卡牌子集) 来把扁平池压到门槛以下——那个手法对
        // 角色卡池不适用（RegentCardPool 是硬编码类型列表，不随 ModelDb 注册集合缩小），
        // 所以改为直接锁"口径换了"这件事本身，外加无玩家时的边界。
        var runState = new RunState("scroll-boxes-gate", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());

        IReadOnlyList<CardModel> characterPool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, isMultiplayer: false)
            .ToArray();
        int flatNonColorlessCount = CardPoolFilters.ForSinglePlayer(ModelDb.All<CardModel>())
            .Count(card => !card.IsColorless);

        // 换口径的证据：角色卡池严格小于"全部非无色卡"（后者还含其他角色的卡）。
        Assert.True(characterPool.Count < flatNonColorlessCount);
        Assert.True(characterPool.Count(card => card.Rarity == CardRarity.Common) >= 4);
        Assert.True(characterPool.Count(card => card.Rarity == CardRarity.Uncommon) >= 2);
        Assert.True(ModelDb.Relic<ScrollBoxes>().IsAllowedAtNeow(runState));

        // Neow 候选筛选走的是未 AssignOwner 的克隆，无玩家时必须保守拒绝而不是抛 NRE。
        Assert.False(ModelDb.Relic<ScrollBoxes>()
            .IsAllowedAtNeow(new RunState("scroll-boxes-no-players", new Overgrowth())));
    }

    [Fact]
    public async Task AfterObtained_GeneratesTwoUniqueFixedRarityBundlesAndAddsTheFirst()
    {
        const string seed = "scroll-boxes-bundles";
        Player control = CreatePlayer(seed);
        CardModel[] commonPool = control.Character.CardPool
            .GetUnlockedCards(control.UnlockState, isMultiplayer: false)
            .Where(card => card.Rarity == CardRarity.Common).ToArray();
        CardModel[] uncommonPool = control.Character.CardPool
            .GetUnlockedCards(control.UnlockState, isMultiplayer: false)
            .Where(card => card.Rarity == CardRarity.Uncommon).ToArray();
        double[] expectedBounds =
        [
            commonPool.Length, commonPool.Length - 1, uncommonPool.Length,
            commonPool.Length - 2, commonPool.Length - 3, uncommonPool.Length - 1,
        ];
        Rng referenceRng = control.PlayerRng.Rewards.CloneExact();
        var expectedIds = new List<ModelId>();
        // Independent source-level oracle: filter the character pool by rarity and remove every used ID.
        for (int bundleIndex = 0; bundleIndex < 2; bundleIndex++)
        {
            for (int commonIndex = 0; commonIndex < 2; commonIndex++)
            {
                CardModel[] remaining = commonPool.Where(card => !expectedIds.Contains(card.Id)).ToArray();
                expectedIds.Add(remaining[referenceRng.NextInt(0, remaining.Length)].Id);
            }

            CardModel[] uncommonRemaining = uncommonPool.Where(card => !expectedIds.Contains(card.Id)).ToArray();
            expectedIds.Add(uncommonRemaining[referenceRng.NextInt(0, uncommonRemaining.Length)].Id);
        }

        int rewardsCounterBefore = control.PlayerRng.Rewards.Counter;
        var observedBounds = new List<double>();
        RngDrawObserver? previousObserver = RngDiagnostics.DrawObserver;
        try
        {
            RngDiagnostics.DrawObserver = (rng, op, _, max, arity) =>
            {
                if (ReferenceEquals(rng, control.PlayerRng.Rewards) && op == "NextInt" && arity == 2)
                    observedBounds.Add(max);
            };
            List<IReadOnlyList<CardModel>> bundles = ScrollBoxes.GenerateRandomBundles(control);
            Assert.Equal(2, bundles.Count);
            Assert.All(bundles, bundle => Assert.Equal(
                new[] { CardRarity.Common, CardRarity.Common, CardRarity.Uncommon },
                bundle.Select(card => card.Rarity)));
            CardModel[] allGenerated = bundles.SelectMany(bundle => bundle).ToArray();
            Assert.Equal(6, allGenerated.Length);
            Assert.Equal(6, allGenerated.Select(card => card.Id).Distinct().Count());
            Assert.Equal(expectedIds, allGenerated.Select(card => card.Id));
        }
        finally
        {
            RngDiagnostics.DrawObserver = previousObserver;
        }

        Assert.Equal(expectedBounds, observedBounds);
        Assert.Equal(rewardsCounterBefore + 6, control.PlayerRng.Rewards.Counter);
        Assert.Equal(referenceRng.ToSerializable(), control.PlayerRng.Rewards.ToSerializable());

        Player player = CreatePlayer(seed);
        int deckCountBefore = player.Deck.Cards.Count;
        await RelicCmd.Obtain(ModelDb.Relic<ScrollBoxes>(), player);

        CardModel[] added = player.Deck.Cards.Skip(deckCountBefore).ToArray();
        Assert.Equal(3, added.Length);
        Assert.Equal(
            expectedIds.Take(3),
            added.Select(card => card.Id));
        Assert.Equal(2, added.Count(card => card.Rarity == CardRarity.Common));
        Assert.Equal(1, added.Count(card => card.Rarity == CardRarity.Uncommon));
        Assert.All(added, card =>
        {
            Assert.False(card.IsCanonical);
            Assert.Same(player, card.Owner);
            Assert.Same(player.Deck, card.Pile);
        });
        Assert.Equal(control.PlayerRng.Rewards.ToSerializable(), player.PlayerRng.Rewards.ToSerializable());
    }

    [Fact]
    public void Metadata_MatchesAncientWithoutUponPickupPreview()
    {
        ScrollBoxes relic = ModelDb.Relic<ScrollBoxes>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.False(relic.HasUponPickupEffect);
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        return player;
    }
}
