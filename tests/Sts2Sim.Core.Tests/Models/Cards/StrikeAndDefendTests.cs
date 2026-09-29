namespace Sts2Sim.Core.Tests.Models.Cards;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class StrikeAndDefendTests
{
    public StrikeAndDefendTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    private sealed class FakeRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => Array.Empty<AbstractModel>();

        public RunRngSet Rng { get; } = new RunRngSet("strike_defend_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState : ICombatState
    {
        public FakeCombatState(IRunState runState) => RunState = runState;

        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);

        public IRunState RunState { get; }

        public IReadOnlyList<Creature> Allies => Array.Empty<Creature>();

        public IReadOnlyList<Creature> Enemies => Array.Empty<Creature>();

        public IReadOnlyList<Creature> Creatures => Array.Empty<Creature>();

        public IReadOnlyList<Player> Players => Array.Empty<Player>();

        public IReadOnlyList<Creature> HittableEnemies => Array.Empty<Creature>();

        public CombatSide CurrentSide { get; set; }

        public int RoundNumber { get; set; } = 1;

        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => Array.Empty<Creature>();

        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) => Array.Empty<Creature>();

        public bool ContainsCreature(Creature creature) => false;

        public bool IsLiveCombat() => true;
    }

    private static Player MakeInCombatPlayer()
    {
        var runState = new FakeRunState();
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.ResetCombatState();
        player.Creature.CombatState = new FakeCombatState(runState);
        player.PlayerCombatState!.Energy = 3;
        return player;
    }

    [Fact]
    public async Task StrikeRegent_DealsSixDamage_AndCostsOneEnergy()
    {
        Player player = MakeInCombatPlayer();
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        Creature target = Creature.CreateStandaloneForTests(30, 30);
        target.CombatState = player.Creature.CombatState;

        await card.PlayAsync(target);

        Assert.Equal(24, target.CurrentHp);
        Assert.Equal(2, player.PlayerCombatState.Energy);
        Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task StrikeRegent_ThrowsWithoutTarget()
    {
        Player player = MakeInCombatPlayer();
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);

        await Assert.ThrowsAsync<ArgumentNullException>(() => card.PlayAsync(target: null));
    }

    [Fact]
    public async Task DefendRegent_GainsFiveBlock_AndCostsOneEnergy()
    {
        Player player = MakeInCombatPlayer();
        var card = (DefendRegent)ModelDb.Card<DefendRegent>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);

        await card.PlayAsync(target: null);

        Assert.Equal(5, player.Creature.Block);
        Assert.Equal(2, player.PlayerCombatState.Energy);
        Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public void Regent_StartingDeck_Has4Strikes4Defends()
    {
        Player player = MakeInCombatPlayer();

        Assert.Equal(4, player.Deck.Cards.OfType<StrikeRegent>().Count());
        Assert.Equal(4, player.Deck.Cards.OfType<DefendRegent>().Count());
        Assert.Equal(10, player.Deck.Cards.Count);
    }
}
