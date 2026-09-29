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
    public void MerchantCharacterSlots_FollowShopRarityRolls_WhenExactRaritiesAreAvailable()
    {
        // The old seed now rolls Common for Power (which has no Common pool); retain the exact-rarity premise.
        const string seed = "merchant-rarity-roll-sequence-2";
        Player expectedPlayer = CreatePlayer(seed);
        CardRarity[] expectedRarities = Enumerable.Range(0, 5)
            .Select(_ =>
            {
                var rarity = expectedPlayer.Odds.CardRarity.RollWithoutChangingFutureOdds(CardRarityOddsType.Shop);
                // The next slot follows CreateForMerchant's mandatory upgrade roll, even at negative odds.
                expectedPlayer.PlayerRng.Rewards.NextFloat();
                return rarity;
            })
            .ToArray();

        Assert.NotEqual(CardRarity.Common, expectedRarities[4]); // Power slot must have the rolled rarity.
        MerchantInventory inventory = MerchantInventory.Generate(CreatePlayer(seed));

        Assert.Equal(expectedRarities, inventory.Cards.Take(5).Select(entry => entry.Card.Rarity));
    }
    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
