using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Ascension;

[Collection("ModelDb")]
public sealed class AscensionManagerApplyEffectsTests : IDisposable
{
    public AscensionManagerApplyEffectsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData(3, Player.InitialMaxPotionSlotCount, 0)]
    [InlineData(4, Player.InitialMaxPotionSlotCount - 1, 0)]
    [InlineData(5, Player.InitialMaxPotionSlotCount - 1, 1)]
    public void ApplyEffectsTo_UsesA4AndA5Boundaries(int level, int slots, int banes)
    {
        Player player = CreatePlayer(0);

        new AscensionManager(level).ApplyEffectsTo(player);

        Assert.Equal(slots, player.MaxPotionCount);
        Assert.Equal(banes, CountBanes(player));
    }

    [Fact]
    public void CreateForNewRun_A5AppliesEffectsExactlyOnceAndAppendsOwnedBane()
    {
        Player player = CreatePlayer(5);
        AscendersBane bane = Assert.Single(player.Deck.Cards.OfType<AscendersBane>());
        Assert.Equal(Player.InitialMaxPotionSlotCount - 1, player.MaxPotionCount);
        Assert.Same(player, bane.Owner);
        Assert.Same(player.Deck, bane.Pile);
        Assert.Same(bane, player.Deck.Cards[^1]);
        Assert.True(bane.IsMutable);
        Assert.Equal(1, bane.FloorAddedToDeck);
        Assert.NotNull(player.PlayerRng);
        Assert.NotNull(player.Odds);
        Assert.NotNull(player.RelicGrabBag);
    }

    private static Player CreatePlayer(int level) =>
        Player.CreateForNewRun(ModelDb.Character<Regent>(), new FakeRunState(level));

    private static int CountBanes(Player player) => player.Deck.Cards.OfType<AscendersBane>().Count();

    private sealed class FakeRunState(int level) : IRunState
    {
        public AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            Array.Empty<AbstractModel>();
        public RunRngSet Rng { get; } = new("ascension_apply_effects_tests");
        public AscensionManager Ascension { get; } = new(level);
        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }
}
