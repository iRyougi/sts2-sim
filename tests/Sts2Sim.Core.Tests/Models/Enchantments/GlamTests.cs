using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Enchantments;

[Collection("ModelDb")]
public sealed class GlamTests : IDisposable
{
    public GlamTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task FirstPlay_ReplaysExactlyOnce_ThenDisablesForCombat()
    {
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync("task11-glam");
        Peck card = Task11CombatTestSupport.AddToHand<Peck>(player);
        await CardCmd.Enchant<Glam>(card, 1m);
        var glam = Assert.IsType<Glam>(Assert.Single(card.Enchantments));
        decimal hpBefore = room.Engine.State.Enemies[0].CurrentHp;

        await card.PlayAsync(room.Engine.State.Enemies[0]);

        Assert.Equal(12, hpBefore - room.Engine.State.Enemies[0].CurrentHp);
        Assert.Equal(EnchantmentStatus.Disabled, glam.Status);

        CardPileCmd.Add(card, Sts2Sim.Core.Entities.Cards.PileType.Hand);
        decimal beforeSecondPlay = room.Engine.State.Enemies[0].CurrentHp;
        await card.PlayAsync(room.Engine.State.Enemies[0]);

        Assert.Equal(6, beforeSecondPlay - room.Engine.State.Enemies[0].CurrentHp);
    }

    [Fact]
    public async Task FreshCombatClone_ResetsToNormal()
    {
        (var player, _) = await Task11CombatTestSupport.CreateCombatAsync("task11-glam-reset");
        Peck card = Task11CombatTestSupport.AddToHand<Peck>(player);
        await CardCmd.Enchant<Glam>(card, 1m);
        var glam = Assert.IsType<Glam>(card.Enchantments.Single());
        _ = glam.EnchantPlayCount(1);

        var freshClone = (Peck)card.MutableClone();

        Assert.Equal(EnchantmentStatus.Normal,
            Assert.IsType<Glam>(freshClone.Enchantments.Single()).Status);
    }
}
