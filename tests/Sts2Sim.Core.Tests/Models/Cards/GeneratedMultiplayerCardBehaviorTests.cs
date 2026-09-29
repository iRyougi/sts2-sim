using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedMultiplayerCardBehaviorTests : IDisposable
{
    public GeneratedMultiplayerCardBehaviorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task UpgradedBelieveInYou_GrantsThreeEnergyToSelectedAllyOnly()
    {
        (Player owner, Player ally, _) = await CreateTwoPlayerCombatAsync("believe-target");
        BelieveInYou card = AddToHand<BelieveInYou>(owner);
        card.Upgrade();
        owner.PlayerCombatState!.Energy = 0;
        ally.PlayerCombatState!.Energy = 0;

        await card.PlayAsync(ally.Creature);

        Assert.Equal(0, owner.PlayerCombatState.Energy);
        Assert.Equal(3, ally.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task UpgradedCoordinate_AppliesEightTemporaryStrengthToSelectedAlly()
    {
        (Player owner, Player ally, CombatRoom room) = await CreateTwoPlayerCombatAsync("coordinate-target");
        Coordinate card = AddToHand<Coordinate>(owner);
        card.Upgrade();

        await card.PlayAsync(ally.Creature);

        Assert.Null(owner.Creature.GetPower<CoordinatePower>());
        Assert.Equal(8m, ally.Creature.GetPower<CoordinatePower>()!.Amount);
        Assert.Equal(8m, ally.Creature.GetPower<StrengthPower>()!.Amount);

        await Hook.AfterSideTurnEnd(room.Engine.State, CombatSide.Player, room.Engine.State.Allies);

        Assert.Null(ally.Creature.GetPower<CoordinatePower>());
        Assert.Null(ally.Creature.GetPower<StrengthPower>());
    }

    [Fact]
    public async Task UpgradedHuddleUpAndPlot_BroadcastToEveryLivingPlayerAlly()
    {
        (Player drawOwner, Player drawAlly, _) = await CreateTwoPlayerCombatAsync("huddle-broadcast");
        ClearHand(drawOwner);
        ClearHand(drawAlly);
        HuddleUp huddle = AddToHand<HuddleUp>(drawOwner);
        huddle.Upgrade();
        drawOwner.PlayerCombatState!.Energy = 1;

        await huddle.PlayAsync(target: null);

        Assert.Equal(3, drawOwner.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(3, drawAlly.PlayerCombatState!.Hand.Cards.Count);

        (Player plotOwner, Player plotAlly, _) = await CreateTwoPlayerCombatAsync("plot-broadcast");
        Plot plot = AddToHand<Plot>(plotOwner);
        plot.Upgrade();

        await plot.PlayAsync(target: null);

        Assert.Equal(3m, plotOwner.Creature.GetPower<DrawCardsNextTurnPower>()!.Amount);
        Assert.Equal(3m, plotAlly.Creature.GetPower<DrawCardsNextTurnPower>()!.Amount);
    }

    [Fact]
    public async Task UpgradedResonance_GrantsTwoStrengthAndReducesEveryEnemyByOne()
    {
        (Player owner, _, CombatRoom room) = await CreateTwoPlayerCombatAsync("resonance-all-enemies");
        room.Engine.State.AddMonster(
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            CombatSide.Enemy);
        Resonance card = AddToHand<Resonance>(owner);
        card.Upgrade();
        owner.PlayerCombatState!.Energy = 1;
        await PlayerCmd.GainStars(2, owner);

        await card.PlayAsync(target: null);

        Assert.Equal(2m, owner.Creature.GetPower<StrengthPower>()!.Amount);
        Assert.All(
            room.Engine.State.Enemies,
            enemy => Assert.Equal(-1m, enemy.GetPower<StrengthPower>()!.Amount));
    }

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private static async Task<(Player owner, Player ally, CombatRoom room)> CreateTwoPlayerCombatAsync(
        string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player ally = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(ally);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (owner, ally, room);
    }
}