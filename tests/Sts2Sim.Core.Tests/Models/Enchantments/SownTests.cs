using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Enchantments;

[Collection("ModelDb")]
public sealed class SownTests : IDisposable
{
    public SownTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task FirstPlay_GainsMagnitudeEnergyAndDisables_ForLaterPlays()
    {
        (var player, _) = await Task11CombatTestSupport.CreateCombatAsync("task11-sown");
        StrikeRegent card = Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
        await CardCmd.Enchant<Sown>(card, 2m);
        var sown = Assert.IsType<Sown>(Assert.Single(card.Enchantments));
        int initialEnergy = player.PlayerCombatState!.Energy;

        await card.PlayAsync(player.Creature.CombatState!.Enemies[0]);

        Assert.Equal(initialEnergy - 1 + 2, player.PlayerCombatState.Energy);
        Assert.Equal(EnchantmentStatus.Disabled, sown.Status);

        CardPileCmd.Add(card, PileType.Hand);
        await card.PlayAsync(player.Creature.CombatState.Enemies[0]);

        Assert.Equal(initialEnergy, player.PlayerCombatState.Energy);

        var freshClone = (StrikeRegent)card.MutableClone();
        Assert.Equal(
            EnchantmentStatus.Normal,
            Assert.IsType<Sown>(freshClone.Enchantments.Single()).Status);
    }
}
