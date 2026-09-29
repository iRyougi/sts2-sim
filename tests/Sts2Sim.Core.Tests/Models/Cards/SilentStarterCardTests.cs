namespace Sts2Sim.Core.Tests.Models.Cards;

using Sts2Sim.Core.Combat;
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

[Collection("ModelDb")]
public sealed class SilentStarterCardTests : IDisposable
{
    public SilentStarterCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Silent), typeof(StrikeSilent), typeof(DefendSilent), typeof(Neutralize),
            typeof(Survivor), typeof(RingOfTheSnake), typeof(WeakPower), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(false, 6)]
    [InlineData(true, 9)]
    public async Task StrikeSilent_DealsOfficialDamage(bool upgraded, int expectedDamage)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"silent-strike-{upgraded}");
        StrikeSilent card = AddToHand<StrikeSilent>(player, upgraded);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int startingHp = enemy.CurrentHp;

        await room.Engine.PlayCardAsync(player, card, enemy);

        Assert.Equal(startingHp - expectedDamage, enemy.CurrentHp);
    }

    [Theory]
    [InlineData(false, 5)]
    [InlineData(true, 8)]
    public async Task DefendSilent_GainsOfficialBlock(bool upgraded, int expectedBlock)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"silent-defend-{upgraded}");
        DefendSilent card = AddToHand<DefendSilent>(player, upgraded);

        await room.Engine.PlayCardAsync(player, card, target: null);

        Assert.Equal(expectedBlock, player.Creature.Block);
    }

    [Theory]
    [InlineData(false, 3, 1)]
    [InlineData(true, 4, 2)]
    public async Task Neutralize_DealsDamageThenAppliesWeak(
        bool upgraded,
        int expectedDamage,
        int expectedWeak)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"silent-neutralize-{upgraded}");
        Neutralize card = AddToHand<Neutralize>(player, upgraded);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int startingHp = enemy.CurrentHp;

        await room.Engine.PlayCardAsync(player, card, enemy);

        Assert.Equal(startingHp - expectedDamage, enemy.CurrentHp);
        Assert.Equal(expectedWeak, Assert.Single(enemy.Powers.OfType<WeakPower>()).Amount);
    }

    [Theory]
    [InlineData(false, 8)]
    [InlineData(true, 11)]
    public async Task Survivor_GainsBlockThenDiscardsSelectedHandCard(
        bool upgraded,
        int expectedBlock)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"silent-survivor-{upgraded}");
        Survivor survivor = AddToHand<Survivor>(player, upgraded);
        StrikeSilent first = AddToHand<StrikeSilent>(player);
        StrikeSilent selected = AddToHand<StrikeSilent>(player);
        room.Engine.State.CardSelectionSource = new FixedSelectionSource(selected);

        await room.Engine.PlayCardAsync(player, survivor, target: null);

        Assert.Equal(expectedBlock, player.Creature.Block);
        Assert.Contains(player.PlayerCombatState!.Hand.Cards, card => ReferenceEquals(card, first));
        Assert.DoesNotContain(player.PlayerCombatState.Hand.Cards, card => ReferenceEquals(card, selected));
        Assert.Contains(player.PlayerCombatState.DiscardPile.Cards, card => ReferenceEquals(card, selected));
    }

    private static TCard AddToHand<TCard>(Player player, bool upgraded = false)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        if (upgraded)
        {
            card.Upgrade();
        }
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToArray())
            {
                CardPileCmd.Remove(card);
            }
        }
        player.PlayerCombatState.Energy = 3;
        return (player, room);
    }

    private sealed class FixedSelectionSource(CardModel selected) : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            Assert.Contains(selected, request.Candidates);
            Assert.Equal((1, 1), (request.MinCount, request.MaxCount));
            return Task.FromResult<IReadOnlyList<CardModel>>(new[] { selected });
        }
    }
}
