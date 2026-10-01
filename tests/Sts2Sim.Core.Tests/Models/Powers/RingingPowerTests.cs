using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Models.Afflictions;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class RingingPowerTests : IDisposable
{
    public RingingPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight), typeof(WanderingGrunt), typeof(RingingPower), typeof(Ringing), typeof(BeatDown), typeof(ThrowingRingingCard),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task MarksExistingAndEnteringCards_AllowsOnlyFirstPlay_AndClearsAtOwnerTurnEnd()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync();
        CardModel[] existingCards = player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToArray();
        RingingPower ringing = Assert.IsType<RingingPower>(
            await PowerCmd.Apply<RingingPower>(room.Engine.State, player.Creature, 1m, null, null));

        Assert.All(existingCards, card => Assert.True(card.Affliction is Ringing));

        DefendRegent first = CreateOwnedDefend(player);
        DefendRegent second = CreateOwnedDefend(player);
        await CardPileCmd.Generate(room.Engine.State, first, PileType.Hand);
        await CardPileCmd.Generate(room.Engine.State, second, PileType.Hand);

        Assert.True(first.Affliction is Ringing);
        Assert.True(second.Affliction is Ringing);
        player.PlayerCombatState.Energy = 3;
        Assert.True(first.CanPlay(out UnplayableReason firstReason));
        Assert.Equal(UnplayableReason.None, firstReason);

        await room.Engine.PlayCardAsync(player, first, target: null);

        Assert.False(second.CanPlay(out UnplayableReason secondReason));
        Assert.True(secondReason.HasFlag(UnplayableReason.BlockedByHook));
        int energyBeforeBlockedPlay = player.PlayerCombatState.Energy;
        CardPile? pileBeforeBlockedPlay = second.Pile;

        await room.Engine.PlayCardAsync(player, second, target: null);

        Assert.Equal(energyBeforeBlockedPlay, player.PlayerCombatState.Energy);
        Assert.Same(pileBeforeBlockedPlay, second.Pile);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Null(player.Creature.GetPower<RingingPower>());
        Assert.All(existingCards.Append(first).Append(second), card => Assert.False(card.Affliction is Ringing));
        Assert.True(second.CanPlay(out UnplayableReason afterRemovalReason));
        Assert.Equal(UnplayableReason.None, afterRemovalReason);
    }

    [Fact]
    public async Task AutoPlayCmd_RingingAllowsOnlyFirstCardWithoutSpendingResourcesOrStrandingCards()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync();
        await PowerCmd.Apply<RingingPower>(room.Engine.State, player.Creature, 1m, null, null);
        DefendRegent first = CreateOwnedDefend(player);
        DefendRegent second = CreateOwnedDefend(player);
        await CardPileCmd.Generate(room.Engine.State, first, PileType.Draw, CardPilePosition.Top);
        await CardPileCmd.Generate(room.Engine.State, second, PileType.Draw, CardPilePosition.Top);
        int blockBefore = player.Creature.Block;
        int energyBefore = player.PlayerCombatState!.Energy;

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, count: 2);

        Assert.Equal(blockBefore + 5, player.Creature.Block);
        Assert.Equal(energyBefore, player.PlayerCombatState.Energy);
        Assert.Equal(1, player.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Equal(PileType.Discard, first.Pile!.Type);
        Assert.Equal(PileType.Discard, second.Pile!.Type);
        Assert.Empty(player.PlayerCombatState.PlayPile.Cards);
    }

    [Fact]
    public async Task BeatDown_RingingBlocksNestedAutoPlaysBeforeTheySpendResourcesOrLeaveDiscard()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync();
        StrikeRegent[] nestedAttacks = Enumerable.Range(0, 3)
            .Select(_ => CreateOwned<StrikeRegent>(player))
            .ToArray();
        foreach (StrikeRegent attack in nestedAttacks)
        {
            CardPileCmd.Add(attack, PileType.Discard);
        }

        BeatDown beatDown = CreateOwned<BeatDown>(player);
        CardPileCmd.Add(beatDown, PileType.Hand);
        await PowerCmd.Apply<RingingPower>(room.Engine.State, player.Creature, 1m, null, null);
        player.PlayerCombatState!.Energy = 10;
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await room.Engine.PlayCardAsync(player, beatDown, target: null);

        Assert.Equal(hpBefore, enemy.CurrentHp);
        Assert.Equal(7, player.PlayerCombatState.Energy);
        Assert.Equal(1, player.PlayerCombatState.CardsPlayedThisTurn);
        Assert.All(nestedAttacks, attack => Assert.Equal(PileType.Discard, attack.Pile!.Type));
    }

    [Fact]
    public async Task FailedFirstPlay_RemainsStartedAndKeepsGateClosed()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync();
        ThrowingRingingCard failing = CreateOwned<ThrowingRingingCard>(player);
        DefendRegent next = CreateOwnedDefend(player);
        CardPileCmd.Add(failing, PileType.Hand);
        CardPileCmd.Add(next, PileType.Hand);
        await PowerCmd.Apply<RingingPower>(room.Engine.State, player.Creature, 1m, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.PlayAsync(target: null));

        Assert.Equal(0, player.PlayerCombatState!.CardsPlayedThisTurn);
        Assert.Equal(1, player.PlayerCombatState.CardPlaysStartedThisTurn);
        Assert.False(next.CanPlay(out UnplayableReason reason));
        Assert.True(reason.HasFlag(UnplayableReason.BlockedByHook));

        await next.PlayAsync(target: null);
        Assert.Equal(1, player.PlayerCombatState.CardPlaysStartedThisTurn);
        Assert.Equal(PileType.Hand, next.Pile!.Type);
    }

    private static DefendRegent CreateOwnedDefend(Player player)
    {
        var card = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static TCard CreateOwned<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private sealed class ThrowingRingingCard : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Common;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 0;

        protected override Task OnPlay(CardPlay cardPlay) =>
            throw new InvalidOperationException("Ringing started-play failure probe.");
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync()
    {
        var runState = new RunState("ringing-power-tests", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
