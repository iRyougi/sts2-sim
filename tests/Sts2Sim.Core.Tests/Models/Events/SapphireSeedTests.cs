using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class SapphireSeedTests : IDisposable
{
    public SapphireSeedTests()
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
            typeof(Sown),
            typeof(SapphireSeed),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Eat_HealsNineAndUpgradesTheFirstEligibleDeckCard()
    {
        (RunState runState, Player player, SapphireSeed ev) = Setup("sapphire-eat");
        await CreatureCmd.LoseHp(
            runState,
            player.Creature,
            20m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        int hpBefore = player.Creature.CurrentHp;
        CardModel expectedUpgrade = player.Deck.Cards.First(card => card.IsUpgradable);

        await Choose(ev, "EAT");

        Assert.Equal(hpBefore + 9, player.Creature.CurrentHp);
        Assert.True(expectedUpgrade.IsUpgraded);
        Assert.All(
            player.Deck.Cards.Where(card => !ReferenceEquals(card, expectedUpgrade)),
            card => Assert.False(card.IsUpgraded));
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Eat_WithNoUpgradableCards_StillHealsAndFinishes()
    {
        (RunState runState, Player player, SapphireSeed ev) = Setup("sapphire-eat-empty");
        foreach (CardModel card in player.Deck.Cards.Where(card => card.IsUpgradable))
        {
            CardCmd.Upgrade(card);
        }
        await CreatureCmd.LoseHp(
            runState,
            player.Creature,
            20m,
            ValueProp.Unblockable | ValueProp.Unpowered);
        int hpBefore = player.Creature.CurrentHp;

        await Choose(ev, "EAT");

        Assert.Equal(hpBefore + 9, player.Creature.CurrentHp);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Plant_EnchantsTheFirstSownCandidateWithMagnitudeOne()
    {
        (_, Player player, SapphireSeed ev) = Setup("sapphire-plant");
        CardModel expected = player.Deck.Cards[0];

        await Choose(ev, "PLANT");

        var sown = Assert.IsType<Sown>(Assert.Single(expected.Enchantments));
        Assert.Equal(1m, sown.Magnitude);
        Assert.All(player.Deck.Cards.Skip(1), card => Assert.Empty(card.Enchantments));
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task Plant_WithNoSownCandidate_FinishesWithoutChangingEnchantments()
    {
        (_, Player player, SapphireSeed ev) = Setup("sapphire-plant-empty");
        foreach (CardModel card in player.Deck.Cards)
        {
            await CardCmd.Enchant<Sown>(card, 2m);
        }
        EnchantmentModel[] enchantmentsBefore = player.Deck.Cards
            .Select(card => card.Enchantments.Single())
            .ToArray();

        await Choose(ev, "PLANT");

        Assert.Equal(
            enchantmentsBefore,
            player.Deck.Cards.Select(card => card.Enchantments.Single()));
        Assert.All(enchantmentsBefore, enchantment => Assert.Equal(2m, enchantment.Magnitude));
        Assert.True(ev.IsFinished);
    }

    private static (RunState RunState, Player Player, SapphireSeed Event) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.ConfigureCardSelectionSource(new LegacySelectionDecisionSource());
        var ev = (SapphireSeed)ModelDb.Event<SapphireSeed>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(SapphireSeed ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
