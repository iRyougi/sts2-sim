using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Exceptions;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class PhialHolsterTests : IDisposable
{
    public PhialHolsterTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task TryToProcure_RequiresMutableOwnershipAndReturnsFalseOnlyForFullSlots()
    {
        Player player = CreatePlayer("phial-procure");
        Player foreign = CreatePlayer("phial-procure-foreign");
        PotionModel canonical = ModelDb.Potion<StrengthPotion>();

        await Assert.ThrowsAsync<CanonicalModelException>(() => PotionCmd.TryToProcure(canonical, player));

        var mutable = (PotionModel)canonical.MutableClone();
        Assert.True(await PotionCmd.TryToProcure(mutable, player));
        Assert.Same(mutable, player.PotionSlots[0]);
        Assert.Same(player, mutable.Owner);

        PotionModel foreignOwned = foreign.AddPotionInternal(canonical);
        while (player.PotionSlots.Contains(null))
        {
            player.AddPotionInternal(canonical);
        }
        PotionModel?[] fullSlots = player.PotionSlots.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() => PotionCmd.TryToProcure(foreignOwned, player));
        var unowned = (PotionModel)canonical.MutableClone();
        Assert.False(await PotionCmd.TryToProcure(unowned, player));
        Assert.Null(unowned.Owner);
        Assert.Equal(fullSlots, player.PotionSlots);
    }

    [Fact]
    public async Task TryToProcure_AutomaticPotionRemainsOwnedInItsSlotWithoutUsing()
    {
        Player player = CreatePlayer("phial-procure-automatic");
        FairyInABottle canonical = ModelDb.Potion<FairyInABottle>();
        Assert.Equal(PotionUsage.Automatic, canonical.Usage);
        var automatic = (FairyInABottle)canonical.MutableClone();

        Assert.True(await PotionCmd.TryToProcure(automatic, player));

        Assert.Same(automatic, Assert.Single(player.PotionSlots.OfType<PotionModel>()));
        Assert.Same(player, automatic.Owner);
    }

    [Fact]
    public async Task AfterObtained_GrowsOneSlotAndProcuresTwoDistinctFlatPoolPotions()
    {
        Player player = CreatePlayer("phial-two-potions");
        int maxBefore = player.MaxPotionCount;
        int rngBefore = player.RunState.Rng.CombatPotionGeneration.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<PhialHolster>(), player);

        Assert.Equal(maxBefore + 1, player.MaxPotionCount);
        PotionModel[] potions = player.PotionSlots.OfType<PotionModel>().ToArray();
        Assert.Equal(2, potions.Length);
        Assert.Equal(2, potions.Select(potion => potion.Id).Distinct().Count());
        Assert.All(potions, potion =>
        {
            Assert.NotEqual(PotionRarity.Event, potion.Rarity);
            Assert.False(potion.IsCanonical);
            Assert.Same(player, potion.Owner);
        });
        Assert.Equal(rngBefore + 4, player.RunState.Rng.CombatPotionGeneration.Counter);
    }

    [Fact]
    public async Task AfterObtained_WhenInitialSlotsAreFull_ProcuresOnlyFirstButStillGeneratesTwo()
    {
        Player player = CreatePlayer("phial-full");
        PotionModel canonical = ModelDb.Potion<StrengthPotion>();
        while (player.PotionSlots.Contains(null))
        {
            player.AddPotionInternal(canonical);
        }
        int rngBefore = player.RunState.Rng.CombatPotionGeneration.Counter;

        await RelicCmd.Obtain(ModelDb.Relic<PhialHolster>(), player);

        Assert.Equal(Player.InitialMaxPotionSlotCount + 1, player.MaxPotionCount);
        Assert.Equal(player.MaxPotionCount, player.PotionSlots.OfType<PotionModel>().Count());
        Assert.Equal(rngBefore + 4, player.RunState.Rng.CombatPotionGeneration.Counter);
    }

    [Fact]
    public void Metadata_MatchesAncientUponPickupBehavior()
    {
        PhialHolster relic = ModelDb.Relic<PhialHolster>();

        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
