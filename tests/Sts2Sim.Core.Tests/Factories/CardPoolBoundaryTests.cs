using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Factories;

[Collection("ModelDb")]
public sealed class CardPoolBoundaryTests : IDisposable
{
    public CardPoolBoundaryTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CharacterRewards_NeverOfferColorlessOrTokenCards()
    {
        var offered = new List<CardModel>();
        for (int i = 0; i < 100; i++)
        {
            Player player = CreatePlayer($"reward-pool-{i}");
            offered.AddRange(CardFactory.CreateForReward(
                player,
                optionCount: 3,
                CardRarityOddsType.RegularEncounter));
        }

        Assert.NotEmpty(offered);
        Assert.All(offered, card =>
        {
            Assert.False(card.IsColorless);
            Assert.NotEqual(CardRarity.Token, card.Rarity);
        });
    }

    [Fact]
    public void CharacterRewards_OnlyOfferCardsFromTheReceivingPlayersCharacterPool()
    {
        AssertRewardsStayInCharacterPool<Regent>("regent-reward-pool");
        AssertRewardsStayInCharacterPool<Silent>("silent-reward-pool");
    }

    [Fact]
    public void MerchantCharacterSlots_NeverOfferTokenCards()
    {
        var characterCards = new List<CardModel>();
        for (int i = 0; i < 100; i++)
        {
            MerchantInventory inventory = MerchantInventory.Generate(CreatePlayer($"shop-pool-{i}"));
            characterCards.AddRange(inventory.Cards.Take(5).Select(entry => entry.Card));
        }

        Assert.NotEmpty(characterCards);
        Assert.All(characterCards, card => Assert.NotEqual(CardRarity.Token, card.Rarity));
    }

    private static void AssertRewardsStayInCharacterPool<TCharacter>(string seed)
        where TCharacter : CharacterModel
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<TCharacter>(), runState);
        runState.AddPlayer(player);
        HashSet<CardModel> characterPool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, isMultiplayer: false)
            .ToHashSet();

        CardModel[] offered = Enum.GetValues<CardRarity>()
            .Where(rarity => rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .SelectMany(rarity => CardFactory.CreateForReward(player, 100, rarity))
            .ToArray();

        Assert.NotEmpty(offered);
        Assert.All(offered, card => Assert.Contains(card, characterPool));
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
