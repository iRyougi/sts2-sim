using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Merchant;

file sealed class ShelfMerchantCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 99;
    public override IReadOnlyList<Type> StartingDeck => ModelDb.Character<Regent>().StartingDeck;
    public override IReadOnlyList<Type> StartingRelics => ModelDb.Character<Regent>().StartingRelics;
    public override Sts2Sim.Core.Models.CardPools.CardPoolModel CardPool => new ShelfMerchantPool();
}

file sealed class ShelfMerchantPool : Sts2Sim.Core.Models.CardPools.CardPoolModel
{
    public override IReadOnlyList<CardModel> AllCards => [ModelDb.Card<ShopCommonAttackA>(), ModelDb.Card<ShopUncommonAttackB>(), ModelDb.Card<ShopCommonSkillA>(), ModelDb.Card<ShopRareSkillB>(), ModelDb.Card<ShopUncommonPower>()];
}
file sealed class ShopCommonAttackA : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
}

file sealed class ShopUncommonAttackB : CardModel
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
}

file sealed class ShopCommonSkillA : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
}

file sealed class ShopRareSkillB : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
}

file sealed class ShopUncommonPower : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
}

file sealed class MerchantCostRelic(RelicRarity rarity) : RelicModel
{
    public override RelicRarity Rarity => rarity;
}

[Collection("ModelDb")]
public class MerchantInventoryTests : IDisposable
{
    public MerchantInventoryTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(ShelfMerchantCharacter), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
            typeof(Vajra), typeof(Circlet), typeof(StrengthPotion),
            typeof(ShopCommonAttackA), typeof(ShopUncommonAttackB),
            typeof(ShopCommonSkillA), typeof(ShopRareSkillB), typeof(ShopUncommonPower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void MerchantCost_UsesRarityTable_AndMakesNonShopRelicsUnpurchasable()
    {
        Assert.Equal(175, new MerchantCostRelic(RelicRarity.Common).MerchantCost);
        Assert.Equal(225, new MerchantCostRelic(RelicRarity.Uncommon).MerchantCost);
        Assert.Equal(275, new MerchantCostRelic(RelicRarity.Rare).MerchantCost);
        Assert.Equal(200, new MerchantCostRelic(RelicRarity.Shop).MerchantCost);
        Assert.Equal(1, new MerchantCostRelic(RelicRarity.None).MerchantCost);
        Assert.Equal(int.MaxValue, new MerchantCostRelic(RelicRarity.Starter).MerchantCost);
        Assert.Equal(int.MaxValue, new MerchantCostRelic(RelicRarity.Event).MerchantCost);
        Assert.Equal(int.MaxValue, new MerchantCostRelic(RelicRarity.Ancient).MerchantCost);
    }

    [Fact]
    public void Generate_CreatesExpectedShelfShape_AndAppliesOneCardDiscount()
    {
        var run = new RunState("merchant-inventory-shape", new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<ShelfMerchantCharacter>(), run);
        run.AddPlayer(player);
        MerchantInventory inventory = MerchantInventory.Generate(player);

        Assert.Equal(5, inventory.Cards.Count);
        Assert.Equal(3, inventory.Relics.Count);
        Assert.InRange(inventory.Potions.Count, 0, 3);
        // Inflation 修复：A0 移牌基础价为 75。
        Assert.Equal(75, inventory.CardRemoval.Price);
        Assert.All(inventory.Relics, entry => Assert.True(entry.Price > 0));

        Assert.Equal(1, inventory.Cards.Count(entry => IsDiscounted(entry)));
        Assert.All(inventory.Cards.Where(entry => !IsDiscounted(entry)), entry =>
            Assert.InRange(entry.Price, MinPrice(entry.Card), MaxPrice(entry.Card)));
    }

    [Fact]
    public void Generate_WithFullRegistry_FillsAllPotionSlotsAndOffersEveryRarity()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
        var seen = new HashSet<PotionRarity>();

        for (int index = 0; index < 128; index++)
        {
            MerchantInventory inventory = Generate($"merchant-potion-rarities-{index}");

            Assert.Equal(3, inventory.Potions.Count);
            foreach (MerchantPotionEntry entry in inventory.Potions)
            {
                seen.Add(entry.Potion.Rarity);
                (int minimum, int maximum) = entry.Potion.Rarity switch
                {
                    PotionRarity.Common => (48, 52),
                    PotionRarity.Uncommon => (71, 79),
                    PotionRarity.Rare => (95, 105),
                    _ => throw new ArgumentOutOfRangeException(),
                };
                Assert.InRange(entry.Price, minimum, maximum);
            }
        }

        Assert.Equal(
            new[] { PotionRarity.Common, PotionRarity.Uncommon, PotionRarity.Rare },
            seen.OrderBy(rarity => rarity));
    }

    [Fact]
    public void Generate_PreservesCanonicalAndOwnershipBoundaries()
    {
        MerchantInventory inventory = Generate("merchant-inventory-ownership");

        Assert.All(inventory.Cards, entry =>
        {
            CardModel canonical = ModelDb.GetById<CardModel>(entry.Card.Id);
            Assert.NotSame(canonical, entry.Card);
            Assert.True(entry.Card.IsMutable);
            Assert.Same(inventory.Player, entry.Card.Owner);
            Assert.Null(entry.Card.Pile);
        });
        Assert.All(inventory.Relics, entry =>
        {
            Assert.True(entry.Relic.IsCanonical);
            Assert.Null(entry.Relic.Owner);
        });
        Assert.All(inventory.Potions, entry =>
        {
            PotionModel canonical = ModelDb.GetById<PotionModel>(entry.Potion.Id);
            Assert.NotSame(canonical, entry.Potion);
            Assert.True(entry.Potion.IsMutable);
            Assert.Null(entry.Potion.Owner);
        });
    }

    [Fact]
    public void Generate_IsDeterministicForSameRunSeed()
    {
        MerchantInventory first = Generate("merchant-inventory-deterministic");
        MerchantInventory second = Generate("merchant-inventory-deterministic");

        Assert.Equal(first.Cards.Select(entry => (entry.Card.Id, entry.Price)), second.Cards.Select(entry => (entry.Card.Id, entry.Price)));
        Assert.Equal(first.Relics.Select(entry => (entry.Relic.Id, entry.Price)), second.Relics.Select(entry => (entry.Relic.Id, entry.Price)));
        Assert.Equal(first.Potions.Select(entry => (entry.Potion.Id, entry.Price)), second.Potions.Select(entry => (entry.Potion.Id, entry.Price)));
        Assert.Equal(first.CardRemoval.Price, second.CardRemoval.Price);
    }

    [Fact]
    public void Generate_OnlyConsumesRelicsThroughExpectedBackOfBagDraws()
    {
        const string seed = "merchant-inventory-bag";
        var expectedRun = new RunState(seed, new Overgrowth());
        Player expectedPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), expectedRun);
        expectedRun.AddPlayer(expectedPlayer);

        MerchantInventory expectedInventory = MerchantInventory.Generate(expectedPlayer);
        Player actualPlayer = GeneratePlayer(seed);
        MerchantInventory inventory = MerchantInventory.Generate(actualPlayer);

        Assert.All(inventory.Relics, entry => Assert.True(entry.Relic.IsCanonical));
        Assert.Equal(expectedInventory.Relics.Select(entry => (entry.Relic.Id, entry.Price)), inventory.Relics.Select(entry => (entry.Relic.Id, entry.Price)));
        foreach (RelicRarity rarity in new[] { RelicRarity.Common, RelicRarity.Uncommon, RelicRarity.Rare, RelicRarity.Shop })
        {
            Assert.Equal(
                DrainFromBack(expectedPlayer.RelicGrabBag, rarity),
                DrainFromBack(actualPlayer.RelicGrabBag, rarity));
        }
    }

    [Fact]
    public void Generate_AcrossFixedSeedSet_OnlyOffersRelicsAllowedInShops()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);

        IEnumerable<string> seeds = Enumerable.Range(0, 128)
            .Select(index => $"merchant-shop-eligibility-{index}");

        foreach (string seed in seeds)
        {
            MerchantInventory inventory = Generate(seed);

            Assert.All(inventory.Relics, entry =>
                Assert.True(
                    entry.Relic.IsAllowedInShops,
                    $"Seed {seed} offered shop-ineligible relic {entry.Relic.GetType().Name}."));
        }
    }

    [Fact]
    public void CardRemovalPrice_IncreasesWithPlayerRemovalCount()
    {
        Player player = GeneratePlayer("merchant-inventory-removal");
        player.IncrementCardRemovalsUsed();
        player.IncrementCardRemovalsUsed();

        MerchantInventory inventory = MerchantInventory.Generate(player);

        // Inflation 修复：A0 两次移牌后价格为 125。
        Assert.Equal(125, inventory.CardRemoval.Price);
    }

    private static MerchantInventory Generate(string seed) => MerchantInventory.Generate(GeneratePlayer(seed));

    private static Player GeneratePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }

    private static IReadOnlyList<ModelId> DrainFromBack(RelicGrabBag bag, RelicRarity rarity)
    {
        var ids = new List<ModelId>();
        while (bag.PullFromBack(rarity) is RelicModel relic)
        {
            ids.Add(relic.Id);
        }
        return ids;
    }

    private static bool IsDiscounted(MerchantCardEntry entry) => entry.Price < MinPrice(entry.Card);

    private static int MinPrice(CardModel card) => (int)Math.Round(BasePrice(card) * 0.95m);

    private static int MaxPrice(CardModel card) => (int)Math.Round(BasePrice(card) * 1.05m);

    private static int BasePrice(CardModel card) => card.Rarity switch
    {
        CardRarity.Rare => 150,
        CardRarity.Uncommon => 75,
        _ => 50,
    };
}
