using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class DrawTriggerCardTests : IDisposable
{
    public DrawTriggerCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(KinglyKick), typeof(KinglyPunch), typeof(ThrummingHatchet), typeof(Bolas),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task KinglyKick_RedrawnCard_CostsOneLess()
    {
        (Player player, _) = await CreateCombatAsync("kingly-kick");
        var card = (KinglyKick)ModelDb.Card<KinglyKick>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Draw);
        int costBefore = card.EnergyCost;

        await card.AfterCardDrawn(card, fromHandDraw: true);

        Assert.Equal(costBefore - 1, card.EnergyCost);
    }

    [Fact]
    public async Task KinglyPunch_EachDraw_PermanentlyIncreasesDamage()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("kingly-punch");
        var card = (KinglyPunch)ModelDb.Card<KinglyPunch>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Draw);
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await card.AfterCardDrawn(card, fromHandDraw: true);
        await card.AfterCardDrawn(card, fromHandDraw: true);
        int hpBefore = enemy.CurrentHp;
        CardPileCmd.Add(card, PileType.Hand);

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 16, enemy.CurrentHp);
    }

    [Fact]
    public async Task ThrummingHatchet_PlayedLastTurn_ReturnsToHandBeforeNextDraw()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("thrumming-hatchet");
        var card = (ThrummingHatchet)ModelDb.Card<ThrummingHatchet>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await card.PlayAsync(enemy);
        Assert.NotEqual(PileType.Hand, card.Pile!.Type);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(PileType.Hand, card.Pile!.Type);
    }

    [Fact]
    public async Task ThrummingHatchet_NotPlayedLastTurn_StaysWhereItIs()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("thrumming-hatchet-stale");
        var card = (ThrummingHatchet)ModelDb.Card<ThrummingHatchet>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Discard);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(PileType.Discard, card.Pile!.Type);
    }

    [Fact]
    public async Task Bolas_PlayedLastTurn_ReturnsToHandBeforeNextDraw()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("bolas");
        var card = (Bolas)ModelDb.Card<Bolas>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await card.PlayAsync(enemy);
        Assert.NotEqual(PileType.Hand, card.Pile!.Type);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(PileType.Hand, card.Pile!.Type);
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
