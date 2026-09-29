using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class IroncladCharacterTests : IDisposable
{
    public IroncladCharacterTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task StartingRun_MatchesSource()
    {
        // Ironclad.StartingDeck and BurningBlood, v0.111.0.
        Type[] sourceDeck =
        [
            typeof(StrikeIronclad), typeof(StrikeIronclad), typeof(StrikeIronclad),
            typeof(StrikeIronclad), typeof(StrikeIronclad),
            typeof(DefendIronclad), typeof(DefendIronclad),
            typeof(DefendIronclad), typeof(DefendIronclad), typeof(Bash),
        ];

        var run = new RunState("ironclad-start-source", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run);
        run.AddPlayer(player);

        Assert.Equal(80, player.Creature.CurrentHp);
        Assert.Equal(80, player.Creature.MaxHp);
        Assert.Equal(99, player.Gold);
        Assert.Equal(3, player.MaxEnergy);
        Assert.Equal(sourceDeck, player.Deck.Cards.Select(card => card.GetType()));
        Assert.All(player.Deck.Cards, card => Assert.Same(player, card.Owner));

        BurningBlood burningBlood = Assert.IsType<BurningBlood>(Assert.Single(player.Relics));
        Assert.Same(player, burningBlood.Owner);
        await CreatureCmd.SetCurrentHp(player.Creature, 70m);
        await burningBlood.AfterCombatVictory();
        Assert.Equal(76, player.Creature.CurrentHp);
    }
}
