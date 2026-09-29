using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Factories;

[Collection("ModelDb")]
public sealed class CardUpgradeOddsTests : IDisposable
{
    public CardUpgradeOddsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(
        [
            typeof(NonUpgradableRewardCharacter),
            typeof(NonUpgradableRewardCard),
        ]));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void FixedRarityReward_ReturnsCanonicalUnupgradedOption_WithoutUpgradeDraw()
    {
        const string seed = "fixed-rarity-no-upgrade";
        (_, Player expectedPlayer) = CreatePlayer(seed, actIndex: 2);
        List<CardModel> candidates = RewardCandidates(expectedPlayer, CardRarity.Common);
        Rng expectedRewards = expectedPlayer.PlayerRng.Rewards.CloneExact();
        CardModel expected = candidates[expectedRewards.NextInt(candidates.Count)];

        (_, Player player) = CreatePlayer(seed, actIndex: 2);
        int counterBefore = player.PlayerRng.Rewards.Counter;
        CardModel offered = Assert.Single(CardFactory.CreateForReward(player, 1, CardRarity.Common));

        Assert.Same(expected, offered);
        Assert.True(offered.IsCanonical);
        Assert.False(offered.IsUpgraded);
        Assert.Equal(counterBefore + 1, player.PlayerRng.Rewards.Counter);
        Assert.Equal(expectedRewards.NextFloat(), player.PlayerRng.Rewards.NextFloat());
    }

    [Fact]
    public void ActOne_UpgradeOdds_MatchesBaseValue()
    {
        string seed = FindSeedForRegularRewardUpgradeRoll(actIndex: 0, ascensionLevel: 0, 0f, 1f);
        (_, Player player) = CreatePlayer(seed, actIndex: 0);

        CardModel offered = Assert.Single(CardFactory.CreateForReward(player, 1, CardRarityOddsType.RegularEncounter));

        Assert.False(offered.IsUpgraded);
    }

    [Fact]
    public void ActTwo_UpgradeOdds_IncreasedByScaling()
    {
        string seed = FindSeedForRegularRewardUpgradeRoll(actIndex: 1, ascensionLevel: 0, 0f, 0.25f);
        (_, Player player) = CreatePlayer(seed, actIndex: 1);

        CardModel offered = Assert.Single(CardFactory.CreateForReward(player, 1, CardRarityOddsType.RegularEncounter));

        Assert.True(offered.IsUpgraded);
    }

    [Fact]
    public void ActThree_UpgradeOdds_IncreasedTwice()
    {
        string seed = FindSeedForRegularRewardUpgradeRoll(actIndex: 2, ascensionLevel: 0, 0.25f, 0.5f);
        (_, Player player) = CreatePlayer(seed, actIndex: 2);

        CardModel offered = Assert.Single(CardFactory.CreateForReward(player, 1, CardRarityOddsType.RegularEncounter));

        Assert.True(offered.IsUpgraded);
    }

    [Fact]
    public void ScarcityAscension_UsesHalfUpgradeScaling()
    {
        string seed = FindSeedForRegularRewardUpgradeRoll(
            actIndex: 1,
            ascensionLevel: (int)AscensionLevel.Scarcity,
            0.125f,
            0.25f);
        (_, Player player) = CreatePlayer(seed, actIndex: 1, ascensionLevel: (int)AscensionLevel.Scarcity);

        CardModel offered = Assert.Single(CardFactory.CreateForReward(player, 1, CardRarityOddsType.RegularEncounter));

        Assert.False(offered.IsUpgraded);
    }

    [Fact]
    public void RareRewards_DoNotGainUpgradeOddsFromLaterActs()
    {
        (_, Player player) = CreatePlayer("rare-reward-no-act-bonus", actIndex: 2);

        CardModel offered = Assert.Single(CardFactory.CreateForReward(player, 1, CardRarityOddsType.BossEncounter));

        Assert.Equal(CardRarity.Rare, offered.Rarity);
        Assert.False(offered.IsUpgraded);
    }

    [Fact]
    public void UpgradeRoll_ConsumesExpectedRngStream_AfterRarityAndSelectionForEachOption()
    {
        const int optionCount = 3;
        const string seed = "card-upgrade-rng-order";
        (_, Player expectedPlayer) = CreatePlayer(seed, actIndex: 1);
        Rng expectedRewards = expectedPlayer.PlayerRng.Rewards;
        var selectedCanonicals = new HashSet<CardModel>();
        var expectedIds = new List<ModelId>();
        for (int optionIndex = 0; optionIndex < optionCount; optionIndex++)
        {
            CardRarity rarity = expectedPlayer.Odds.CardRarity.Roll(CardRarityOddsType.RegularEncounter);
            List<CardModel> candidates = RewardCandidates(expectedPlayer, rarity)
                .Where(card => !selectedCanonicals.Contains(card))
                .ToList();
            CardModel selected = candidates[expectedRewards.NextInt(candidates.Count)];
            selectedCanonicals.Add(selected);
            expectedIds.Add(selected.Id);
            _ = expectedRewards.NextFloat();
        }

        (_, Player player) = CreatePlayer(seed, actIndex: 1);
        int counterBefore = player.PlayerRng.Rewards.Counter;
        IReadOnlyList<CardModel> options = CardFactory.CreateForReward(
            player,
            optionCount,
            CardRarityOddsType.RegularEncounter);

        Assert.Equal(expectedIds, options.Select(card => card.Id));
        Assert.Equal(counterBefore + (optionCount * 3), player.PlayerRng.Rewards.Counter);
        Assert.Equal(expectedRewards.NextFloat(), player.PlayerRng.Rewards.NextFloat());
        Assert.Equal(
            expectedPlayer.Odds.CardRarity.Roll(CardRarityOddsType.RegularEncounter),
            player.Odds.CardRarity.Roll(CardRarityOddsType.RegularEncounter));
    }

    [Fact]
    public void UpgradeRoll_ConsumesRewardsDrawBeforeCheckingNonUpgradableCard()
    {
        string seed = FindSeedForNonUpgradableRegularReward();
        (_, Player expectedPlayer) = CreateNonUpgradablePlayer(seed);
        _ = expectedPlayer.Odds.CardRarity.Roll(CardRarityOddsType.RegularEncounter);
        Rng expectedRewards = expectedPlayer.PlayerRng.Rewards.CloneExact();
        _ = expectedRewards.NextInt(1);
        _ = expectedRewards.NextFloat();

        (_, Player player) = CreateNonUpgradablePlayer(seed);
        int counterBefore = player.PlayerRng.Rewards.Counter;
        CardModel offered = Assert.Single(CardFactory.CreateForReward(
            player,
            1,
            CardRarityOddsType.RegularEncounter));

        Assert.False(offered.IsUpgradable);
        Assert.Equal(counterBefore + 3, player.PlayerRng.Rewards.Counter);
        Assert.Equal(expectedRewards.NextFloat(), player.PlayerRng.Rewards.NextFloat());
    }

    [Fact]
    public async Task SelectedRewardOption_PreservesUpgradeInDeck()
    {
        string seed = FindSeedForRegularRewardUpgradeRoll(actIndex: 1, ascensionLevel: 0, 0f, 0.25f);
        (RunState runState, Player player) = CreatePlayer(seed, actIndex: 1);
        CardModel offered = Assert.Single(CardFactory.CreateForReward(player, 1, CardRarityOddsType.RegularEncounter));
        var reward = new CardReward(player, new[] { offered });
        reward.Populate(runState);
        int deckSizeBefore = player.Deck.Cards.Count;

        await reward.SelectOption(offered);

        CardModel added = Assert.Single(player.Deck.Cards.Skip(deckSizeBefore));
        Assert.True(offered.IsUpgraded);
        Assert.True(added.IsUpgraded);
    }

    private static string FindSeedForRegularRewardUpgradeRoll(
        int actIndex,
        int ascensionLevel,
        float minimumExclusive,
        float maximumInclusive)
    {
        for (int candidate = 0; candidate < 10_000; candidate++)
        {
            string seed = $"card-upgrade-odds-{actIndex}-{ascensionLevel}-{candidate}";
            (_, Player player) = CreatePlayer(seed, actIndex, ascensionLevel);
            CardRarity rarity = player.Odds.CardRarity.Roll(CardRarityOddsType.RegularEncounter);
            if (rarity == CardRarity.Rare)
            {
                continue;
            }

            List<CardModel> cards = RewardCandidates(player, rarity);
            Rng rewards = player.PlayerRng.Rewards.CloneExact();
            CardModel selected = cards[rewards.NextInt(cards.Count)];
            float upgradeRoll = rewards.NextFloat();
            if (selected.IsUpgradable && upgradeRoll > minimumExclusive && upgradeRoll <= maximumInclusive)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Could not find a deterministic reward RNG seed in the requested interval.");
    }

    private static string FindSeedForNonUpgradableRegularReward()
    {
        for (int candidate = 0; candidate < 10_000; candidate++)
        {
            string seed = $"non-upgradable-reward-{candidate}";
            (_, Player player) = CreateNonUpgradablePlayer(seed);
            if (player.Odds.CardRarity.Roll(CardRarityOddsType.RegularEncounter) == CardRarity.Common)
            {
                return seed;
            }
        }

        throw new InvalidOperationException("Could not find a regular reward seed that rolls Common.");
    }

    private static List<CardModel> RewardCandidates(Player player, CardRarity rarity) =>
        player.Character.CardPool.GetUnlockedCards(player.UnlockState, isMultiplayer: false)
            .Where(card => !card.IsColorless && card.Rarity == rarity)
            .ToList();

    private static (RunState RunState, Player Player) CreatePlayer(
        string seed,
        int actIndex,
        int ascensionLevel = 0) =>
        CreatePlayer<Regent>(seed, actIndex, ascensionLevel);

    private static (RunState RunState, Player Player) CreateNonUpgradablePlayer(string seed) =>
        CreatePlayer<NonUpgradableRewardCharacter>(seed, actIndex: 0);

    private static (RunState RunState, Player Player) CreatePlayer<TCharacter>(
        string seed,
        int actIndex,
        int ascensionLevel = 0)
        where TCharacter : CharacterModel
    {
        var runState = new RunState(seed, [new FakeAct(0), new FakeAct(1), new FakeAct(2)], ascensionLevel);
        Player player = Player.CreateForNewRun(ModelDb.Character<TCharacter>(), runState);
        runState.AddPlayer(player);
        for (int index = 0; index < actIndex; index++)
        {
            runState.AdvanceToNextAct();
        }

        return (runState, player);
    }

    private sealed class FakeAct(int index) : ActDefinition
    {
        private static readonly IReadOnlyList<EncounterDefinition> Encounters =
        [
            new EncounterDefinition(
                (Func<MonsterModel>)(() => throw new NotSupportedException("Reward tests do not enter encounters."))),
        ];

        public override int Index => index;
        public override IReadOnlyList<Type> EventPool => [typeof(Neow)];
        public override IReadOnlyList<Type> AncientPool => [typeof(Neow)];
        public override int BaseNumberOfRooms => 14;
        public override int NumberOfWeakEncounters => 0;
        protected override IReadOnlyList<EncounterDefinition> MonsterEncounters => Encounters;
        protected override IReadOnlyList<EncounterDefinition> EliteEncounters => Encounters;
        protected override IReadOnlyList<EncounterDefinition> BossEncounters => Encounters;
        public override MapPointTypeCounts GetMapPointTypes(Rng mapRng) => new(12, 7);
    }
}

file sealed class NonUpgradableRewardCharacter : CharacterModel
{
    private static NonUpgradableRewardPool Pool { get; } = new();
    public override int StartingHp => 1;
    public override int StartingGold => 0;
    public override CardPoolModel CardPool => Pool;
}

file sealed class NonUpgradableRewardPool : CardPoolModel
{
    public override IReadOnlyList<CardModel> AllCards => [ModelDb.Card<NonUpgradableRewardCard>()];
}

file sealed class NonUpgradableRewardCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    public override int MaxUpgradeLevel => 0;
    protected override int CanonicalEnergyCost => 0;
}
