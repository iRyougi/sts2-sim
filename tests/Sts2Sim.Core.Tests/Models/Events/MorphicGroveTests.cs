using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class MorphicGroveTests : IDisposable
{
    public MorphicGroveTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight),
            typeof(AstralPulse),
            typeof(Begone),
            typeof(Greed),
            typeof(MorphicGrove),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();


    [Fact]
    public void IsAllowed_RequiresOneHundredGoldAndTwoTransformableCards()
    {
        var runState = new RunState("morphic-is-allowed", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        MorphicGrove grove = ModelDb.Event<MorphicGrove>();

        player.Gold = 100;
        Assert.True(grove.IsAllowed(runState));

        player.Gold = 99;
        Assert.False(grove.IsAllowed(runState));

        player.Gold = 100;
        foreach (CardModel card in player.Deck.Cards.Where(card => card.IsTransformable).Skip(2).ToList())
        {
            CardPileCmd.Remove(card);
        }

        Assert.True(grove.IsAllowed(runState));

        CardPileCmd.Remove(player.Deck.Cards.First(card => card.IsTransformable));

        Assert.False(grove.IsAllowed(runState));
    }
    [Fact]
    public async Task Group_LosesAllGold_AndTransformsTheFirstTwoEligibleCards()
    {
        (_, Player player, MorphicGrove ev) = Setup("morphic-group");
        player.Gold = 237;
        CardModel first = player.Deck.Cards[0];
        CardModel second = player.Deck.Cards[1];
        int deckCount = player.Deck.Cards.Count;

        await Choose(ev, "GROUP");

        Assert.Equal(0, player.Gold);
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, first));
        Assert.DoesNotContain(player.Deck.Cards, card => ReferenceEquals(card, second));
        Assert.Equal(deckCount, player.Deck.Cards.Count);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Group_WithNoTransformableCards_StillLosesAllGoldAndFinishes()
    {
        (_, Player player, MorphicGrove ev) = Setup("morphic-group-empty");
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            CardPileCmd.Remove(card);
        }
        var greed = (Greed)ModelDb.Card<Greed>().MutableClone();
        greed.AssignOwner(player);
        CardPileCmd.Add(greed, PileType.Deck);
        player.Gold = 100;

        await Choose(ev, "GROUP");

        Assert.Equal(0, player.Gold);
        Assert.Same(greed, Assert.Single(player.Deck.Cards));
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Loner_IncreasesMaximumHpByExactlyFive()
    {
        (_, Player player, MorphicGrove ev) = Setup("morphic-loner");
        int maxHpBefore = player.Creature.MaxHp;

        await Choose(ev, "LONER");

        Assert.Equal(maxHpBefore + 5, player.Creature.MaxHp);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Loner_WhenWounded_IncreasesMaximumAndCurrentHpByExactlyFive()
    {
        (RunState runState, Player player, MorphicGrove ev) = Setup("morphic-loner-wounded");
        await CreatureCmd.LoseHp(runState, player.Creature, 10m, ValueProp.Unblockable);
        int maxHpBefore = player.Creature.MaxHp;
        int currentHpBefore = player.Creature.CurrentHp;

        await Choose(ev, "LONER");

        Assert.Equal(maxHpBefore + 5, player.Creature.MaxHp);
        Assert.Equal(currentHpBefore + 5, player.Creature.CurrentHp);
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, MorphicGrove Event) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        var ev = (MorphicGrove)ModelDb.Event<MorphicGrove>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(MorphicGrove ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
