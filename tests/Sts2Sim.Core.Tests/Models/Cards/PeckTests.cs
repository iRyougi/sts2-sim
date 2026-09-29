using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class PeckTests : IDisposable
{
    public PeckTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 8)]
    public async Task Play_DealsTwoDamageThreeOrFourTimes(bool upgraded, int expectedDamage)
    {
        (var player, var room) = await Task11CombatTestSupport.CreateCombatAsync(
            $"task11-peck-{upgraded}");
        Peck peck = Task11CombatTestSupport.AddToHand<Peck>(player);
        if (upgraded)
        {
            peck.Upgrade();
        }
        decimal hpBefore = room.Engine.State.Enemies[0].CurrentHp;

        await peck.PlayAsync(room.Engine.State.Enemies[0]);

        Assert.Equal(expectedDamage, hpBefore - room.Engine.State.Enemies[0].CurrentHp);
        Assert.Equal(CardRarity.Event, peck.Rarity);
    }
}
