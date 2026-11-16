using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Factories;

[Collection("ModelDb")]
public sealed class MultiplayerCardPoolTests : IDisposable
{
    private static readonly HashSet<string> MultiplayerOnlyCardNames = new(StringComparer.Ordinal)
    {
        "BeaconOfHope", "BelieveInYou", "BladeSymphony", "Blaze", "Concoct", "Constellation", "Coordinate", "DemonicShield", "Fade", "Flanking", "GangUp",
        "HammerTime", "HuddleUp", "Intercept", "Knockdown", "Largesse", "Lift",
        "Midnight", "Mimic", "Outrage", "Plot", "Rally", "Sneaky", "TagTeam", "Tank", "TheBall", "Tutor",
        "EnergySurge", "Ignition", "Hibernate", "ImitationLearning", "OneForAll",
        "Cacophony", "GlimpseBeyond", "LegionOfBone", "Soulbound", "Underworld",
        "CoveringFire", "Yoga",
    };

    public MultiplayerCardPoolTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void MultiplayerOnlyFlag_DefaultsFalse_AndIsSetForExactlyVerifiedCards()
    {
        var property = typeof(CardModel).GetProperty("IsMultiplayerOnly");

        Assert.NotNull(property);
        foreach (CardModel card in ModelDb.All<CardModel>())
        {
            bool isMultiplayerOnly = Assert.IsType<bool>(property!.GetValue(card));
            Assert.Equal(MultiplayerOnlyCardNames.Contains(card.GetType().Name), isMultiplayerOnly);
        }
    }

    [Fact]
    public void SinglePlayerRewardsAndMerchantCards_ExcludeMultiplayerOnlyAndAncientCards()
    {
        var rewards = new List<CardModel>();
        var merchantCards = new List<CardModel>();
        for (int i = 0; i < 200; i++)
        {
            Player player = CreatePlayer($"multiplayer-pool-{i}");
            rewards.AddRange(Sts2Sim.Core.Factories.CardFactory.CreateForReward(
                player, optionCount: 3, CardRarityOddsType.RegularEncounter));
            merchantCards.AddRange(MerchantInventory.Generate(player).Cards.Select(entry => entry.Card));
        }

        Assert.All(rewards, card => Assert.DoesNotContain(card.GetType().Name, MultiplayerOnlyCardNames));
        Assert.All(merchantCards, card =>
        {
            Assert.DoesNotContain(card.GetType().Name, MultiplayerOnlyCardNames);
            Assert.NotEqual(CardRarity.Ancient, card.Rarity);
        });
    }

    [Fact]
    public void MerchantCharacterSlots_FollowShopRarityRollsAndNativeFallback()
    {
        const string seed = "merchant-rarity-roll-sequence-2";
        Player expectedPlayer = CreatePlayer(seed);
        // v0.113.1 eagerly fills relics and potion shelf before card Populate.
        var shops = expectedPlayer.PlayerRng.Shops;
        for (int i = 0; i < 5; i++) shops.NextFloat(); // two relic rarities, three prices
        for (int i = 0; i < 3; i++) { shops.NextFloat(); shops.NextInt(); } // potion batch
        for (int i = 0; i < 3; i++) shops.NextFloat(); // potion prices
        CardType[] types = [CardType.Attack, CardType.Attack, CardType.Skill, CardType.Skill, CardType.Power];
        CardRarity[] cycle = [CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare];
        CardModel[] pool = expectedPlayer.Character.CardPool.GetUnlockedCards(expectedPlayer.UnlockState, false)
            .Where(card => !card.IsColorless && card.Rarity != CardRarity.Basic).ToArray();
        var used = new HashSet<ModelId>();
        var expected = new List<(ModelId Id, CardRarity Rarity)>();
        for (int slot = 0; slot < types.Length; slot++)
        {
            CardRarity rolled = expectedPlayer.Odds.CardRarity.RollWithoutChangingFutureOdds(CardRarityOddsType.Shop, shops);
            int start = Array.IndexOf(cycle, rolled);
            // Native GetNextAllowedRarity advances Common -> Uncommon -> Rare -> Common
            // until a non-blacklisted card of this type is available. The oracle does
            // not call the production fallback helper.
            CardRarity rarity = Enumerable.Range(0, 3).Select(offset => cycle[(start + offset) % 3])
                .First(candidate => pool.Any(card => card.Type == types[slot] && card.Rarity == candidate && !used.Contains(card.Id)));
            CardModel picked = shops.NextItem(pool.Where(card => card.Type == types[slot] && card.Rarity == rarity && !used.Contains(card.Id)))!;
            used.Add(picked.Id);
            expected.Add((picked.Id, rarity));
            shops.NextFloat(); // mandatory upgrade, including negative odds
            shops.NextFloat(); // ordinary price
            if (slot == 4)
            {
                Assert.Equal(CardRarity.Common, rolled); // frozen seed exercises the missing Common Power case
                Assert.DoesNotContain(pool, card => card.Type == CardType.Power && card.Rarity == CardRarity.Common);
                Assert.Equal(CardRarity.Uncommon, rarity);
            }
        }
        MerchantInventory inventory = MerchantInventory.Generate(CreatePlayer(seed));
        Assert.Equal(expected, inventory.Cards.Take(5).Select(entry => (entry.Card.Id, entry.Card.Rarity)));
    }
    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
