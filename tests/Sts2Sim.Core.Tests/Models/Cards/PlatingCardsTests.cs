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
public sealed class PlatingCardsTests : IDisposable
{
    public PlatingCardsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(NeutronAegis), typeof(EternalArmor), typeof(PlatingPower),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 11)]
    public async Task NeutronAegis_AppliesPlating(bool upgraded, int expected)
    {
        (Player player, _) = await CreateCombatAsync($"neutron-{upgraded}");
        NeutronAegis card = AddToHand<NeutronAegis>(player);
        player.PlayerCombatState!.GainStars(5);
        if (upgraded) card.Upgrade();

        await card.PlayAsync(target: null);

        Assert.Equal(expected, player.Creature.Powers.OfType<PlatingPower>().Single().Amount);
    }

    [Theory]
    [InlineData(false, 9)]
    [InlineData(true, 12)]
    public async Task EternalArmor_AppliesPlating(bool upgraded, int expected)
    {
        (Player player, _) = await CreateCombatAsync($"eternal-{upgraded}");
        EternalArmor card = AddToHand<EternalArmor>(player);
        if (upgraded) card.Upgrade();

        await card.PlayAsync(target: null);

        Assert.Equal(expected, player.Creature.Powers.OfType<PlatingPower>().Single().Amount);
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
