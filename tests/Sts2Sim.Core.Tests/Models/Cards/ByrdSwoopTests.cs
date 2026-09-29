using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class ByrdSwoopTests : IDisposable
{
    public ByrdSwoopTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 14)]
    [InlineData(true, 18)]
    public async Task Play_DealsSourceExactDamage(bool upgraded, int expectedDamage)
    {
        (Player player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            $"task12-swoop-{upgraded}");
        ByrdSwoop card = Task11CombatTestSupport.AddToHand<ByrdSwoop>(player);
        if (upgraded)
        {
            card.Upgrade();
        }
        decimal hpBefore = room.Engine.State.Enemies[0].CurrentHp;

        await card.PlayAsync(room.Engine.State.Enemies[0]);

        Assert.Equal(expectedDamage, hpBefore - room.Engine.State.Enemies[0].CurrentHp);
        Assert.Equal(0, card.EnergyCost);
        Assert.Equal(CardType.Attack, card.Type);
        Assert.Equal(CardRarity.Event, card.Rarity);
        Assert.Equal(TargetType.AnyEnemy, card.TargetType);
    }
}
