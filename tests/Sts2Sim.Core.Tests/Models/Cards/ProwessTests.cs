using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class ProwessTests : IDisposable
{
    public ProwessTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Prowess), typeof(StrengthPower), typeof(DexterityPower),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task Play_AppliesStrengthAndDexterity(bool upgraded, int expectedAmount)
    {
        (Player player, _) = await CreateCombatAsync($"prowess-{upgraded}");
        Prowess card = AddToHand<Prowess>(player);
        if (upgraded)
        {
            card.Upgrade();
        }

        await card.PlayAsync(target: null);

        Assert.Equal(expectedAmount, player.Creature.Powers.OfType<StrengthPower>().Single().Amount);
        Assert.Equal(expectedAmount, player.Creature.Powers.OfType<DexterityPower>().Single().Amount);
    }

    [Fact]
    public async Task Dexterity_IncreasesBlockFromOwnedCard()
    {
        (Player player, _) = await CreateCombatAsync("prowess-block");
        Prowess prowess = AddToHand<Prowess>(player);
        DefendRegent defend = AddToHand<DefendRegent>(player);

        await prowess.PlayAsync(target: null);
        await defend.PlayAsync(target: null);

        Assert.Equal(6, player.Creature.Block);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
