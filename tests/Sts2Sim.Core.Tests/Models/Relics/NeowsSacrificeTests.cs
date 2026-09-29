using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class NeowsSacrificeTests : IDisposable
{
    public NeowsSacrificeTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task AfterObtained_ProcuresOwnedMutableAmbergrisAndAddsOwnedGuilty()
    {
        Player player = CreatePlayer("neows-sacrifice");

        await RelicCmd.Obtain(ModelDb.Relic<NeowsSacrifice>(), player);

        Ambergris ambergris = Assert.Single(player.PotionSlots.OfType<Ambergris>());
        Assert.False(ambergris.IsCanonical);
        Assert.Same(player, ambergris.Owner);
        Guilty guilty = Assert.Single(player.Deck.Cards.OfType<Guilty>());
        Assert.False(guilty.IsCanonical);
        Assert.Same(player, guilty.Owner);
        Assert.Same(player.Deck, guilty.Pile);
        NeowsSacrifice relic = Assert.Single(player.Relics.OfType<NeowsSacrifice>());
        Assert.Equal(RelicRarity.Ancient, relic.Rarity);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task AfterObtained_WhenPotionSlotsAreFull_StillAddsGuilty()
    {
        Player player = CreatePlayer("neows-sacrifice-full");
        while (player.PotionSlots.Contains(null))
        {
            player.AddPotionInternal(ModelDb.Potion<StrengthPotion>());
        }
        PotionModel?[] slotsBefore = player.PotionSlots.ToArray();

        await RelicCmd.Obtain(ModelDb.Relic<NeowsSacrifice>(), player);

        Assert.Equal(slotsBefore, player.PotionSlots);
        Assert.Empty(player.PotionSlots.OfType<Ambergris>());
        Assert.Single(player.Deck.Cards.OfType<Guilty>());
    }

    private static Player CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return player;
    }
}
