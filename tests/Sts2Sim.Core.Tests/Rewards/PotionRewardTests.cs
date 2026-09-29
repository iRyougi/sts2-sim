using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Rewards;

[Collection("ModelDb")]
public class PotionRewardTests : IDisposable
{
    public PotionRewardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight), typeof(StrengthPotion),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CreateRandom_Common_ReturnsMutablePotionClone()
    {
        PotionModel canonical = ModelDb.Potion<StrengthPotion>();

        PotionModel? potion = PotionFactory.CreateRandom(PotionFactory.Rarity.Common, new Rng(20260723u));

        Assert.IsType<StrengthPotion>(potion);
        Assert.NotSame(canonical, potion);
        Assert.False(potion.IsCanonical);
    }

    [Fact]
    public void CreateRandom_Uncommon_ReturnsNullWhenPoolIsEmpty()
    {
        PotionModel? potion = PotionFactory.CreateRandom(PotionFactory.Rarity.Uncommon, new Rng(20260723u));

        Assert.Null(potion);
    }

    [Fact]
    public async Task ExplicitPotionConstructor_RequiresMutablePotionAndPreservesItForTake()
    {
        var runState = new RunState("explicit-potion-reward", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        PotionModel canonical = ModelDb.Potion<StrengthPotion>();
        var potion = (StrengthPotion)canonical.MutableClone();

        var reward = new PotionReward(potion, player);
        int counterBefore = player.PlayerRng.Rewards.Counter;
        reward.Populate(runState);
        await reward.Take();

        Assert.Same(potion, reward.Potion);
        Assert.Equal(counterBefore, player.PlayerRng.Rewards.Counter);
        Assert.Same(potion, Assert.Single(player.PotionSlots, slot => slot is not null));
        Assert.Same(player, potion.Owner);
        Assert.Throws<Sts2Sim.Core.Models.Exceptions.CanonicalModelException>(
            () => new PotionReward(canonical, player));
    }

    [Fact]
    public async Task Take_SlotsMutableOwnedPotionWithoutCanonicalLeakage_AndIsIdempotent()
    {
        (Player player, PotionReward reward) = CreateRewardWithPotion();
        PotionModel potion = Assert.IsType<StrengthPotion>(reward.Potion);
        PotionModel canonical = ModelDb.Potion<StrengthPotion>();

        Task first = reward.Take();
        Task retry = reward.Take();
        await Task.WhenAll(first, retry);

        PotionModel owned = Assert.Single(player.PotionSlots, slot => slot is not null)!;
        Assert.Same(first, retry);
        Assert.Same(potion, owned);
        Assert.NotSame(canonical, owned);
        Assert.False(owned.IsCanonical);
        Assert.Same(player, owned.Owner);
        Assert.True(reward.IsResolved);
    }

    [Fact]
    public async Task FullRegistry_AcrossFixedSeeds_PopulatesTakesAndOwnsEveryRarity()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
        var seen = new HashSet<PotionRarity>();

        for (int index = 0; index < 128; index++)
        {
            var runState = new RunState($"potion-reward-full-{index}", new Overgrowth());
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            var reward = new PotionReward(player);

            reward.Populate(runState);

            Assert.NotNull(reward.Potion);
            PotionModel potion = reward.Potion!;
            seen.Add(potion.Rarity);
            await reward.Take();
            Assert.Same(potion, Assert.Single(player.PotionSlots, slot => slot is not null));
            Assert.Same(player, potion.Owner);
        }

        Assert.Equal(
            new[] { PotionRarity.Common, PotionRarity.Uncommon, PotionRarity.Rare },
            seen.OrderBy(rarity => rarity));
    }

    [Fact]
    public void SilentCombatRewards_DoNotOfferLockedCharacterPotions()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Where(type =>
            !typeof(PotionModel).IsAssignableFrom(type) || type == typeof(PoisonPotion)));
        var run = new RunState("8F9CPYQ6QYEN", ascensionLevel: 10);
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run,
            new PlayerUnlockState([], []));
        run.AddPlayer(player);

        // Upstream PotionReward uses the player's unlocked out-of-combat pool.
        // Poison Potion is locked behind SILENT4_EPOCH; registration alone never offers it.
        for (int offer = 0; offer < 32; offer++)
        {
            var reward = new PotionReward(player);
            reward.Populate(run);
            Assert.Null(reward.Potion);
        }
    }
    private static (Player Player, PotionReward Reward) CreateRewardWithPotion()
    {
        for (int i = 0; i < 100; i++)
        {
            var runState = new RunState($"potion-reward-{i}", new Overgrowth());
            Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
            runState.AddPlayer(player);
            var reward = new PotionReward(player);
            reward.Populate(runState);
            if (reward.Potion is not null)
            {
                return (player, reward);
            }
        }

        throw new InvalidOperationException("Expected a deterministic test seed to roll a common potion reward.");
    }
}
