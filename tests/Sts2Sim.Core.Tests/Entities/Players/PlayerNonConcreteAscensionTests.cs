namespace Sts2Sim.Core.Tests.Entities.Players;

using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class PlayerNonConcreteAscensionTests : IDisposable
{
    public PlayerNonConcreteAscensionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar),
            typeof(AscendersBane),
            typeof(Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void NonConcreteA8Run_UsesRunAscensionForPlayerCardRarityOdds()
    {
        IRunState runState = new NonConcreteRunState(AscensionLevel.ToughEnemies);

        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);

        Assert.Equal(
            runState.Ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.615f, 0.6f),
            player.Odds.CardRarity.RegularCommonOdds);
        Assert.Equal(
            runState.Ascension.GetValueIfAscension(AscensionLevel.Scarcity, 0.005f, 0.01f),
            player.Odds.CardRarity.RarityGrowth);
    }

    private sealed class NonConcreteRunState(AscensionLevel ascensionLevel) : IRunState
    {
        public RunRngSet Rng { get; } = new("player-odds-non-concrete");

        public AscensionManager Ascension { get; } = new(ascensionLevel);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;

        public IEnumerable<AbstractModel> IterateHookListeners(Sts2Sim.Core.Combat.ICombatState? childCombatState) =>
            Array.Empty<AbstractModel>();
    }
}
