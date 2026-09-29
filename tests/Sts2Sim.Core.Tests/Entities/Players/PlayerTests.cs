namespace Sts2Sim.Core.Tests.Entities.Players;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class PlayerTests
{
    public PlayerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    private sealed class FakeRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => Array.Empty<AbstractModel>();
        public RunRngSet Rng { get; } = new RunRngSet("player_tests");
        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);
        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    [Fact]
    public void CreateForNewRun_PopulatesDeckFromCharacterStartingDeck()
    {
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), new FakeRunState());

        Assert.Equal(10, player.Deck.Cards.Count);
        Assert.Equal(4, player.Deck.Cards.OfType<StrikeRegent>().Count());
        Assert.Equal(4, player.Deck.Cards.OfType<DefendRegent>().Count());
        Assert.All(player.Deck.Cards, c => Assert.True(c.IsMutable));
    }

    [Fact]
    public void CreateForNewRun_SetsHpGoldEnergyFromCharacter()
    {
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), new FakeRunState());

        Assert.Equal(75, player.Creature.CurrentHp);
        Assert.Equal(75, player.Creature.MaxHp);
        Assert.Equal(99, player.Gold);
        Assert.Equal(3, player.MaxEnergy);
    }

    [Fact]
    public void PopulateCombatState_ClonesDeckIntoDrawPile_AndShufflesDeterministically()
    {
        Player p1 = Player.CreateForNewRun(ModelDb.Character<Regent>(), new FakeRunState());
        Player p2 = Player.CreateForNewRun(ModelDb.Character<Regent>(), new FakeRunState());
        p1.ResetCombatState();
        p2.ResetCombatState();

        p1.PopulateCombatState(new Rng(7uL));
        p2.PopulateCombatState(new Rng(7uL));

        Assert.Equal(10, p1.PlayerCombatState!.DrawPile.Cards.Count);
        Assert.Equal(
            p1.PlayerCombatState.DrawPile.Cards.Select(c => c.GetType()),
            p2.PlayerCombatState!.DrawPile.Cards.Select(c => c.GetType()));
        // 战斗内克隆体与牌库原件是不同实例
        Assert.DoesNotContain(p1.PlayerCombatState.DrawPile.Cards, c => p1.Deck.Cards.Contains(c));
    }
}
