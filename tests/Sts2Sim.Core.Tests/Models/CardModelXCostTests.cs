using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class CardModelXCostTests : IDisposable
{
    public CardModelXCostTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(WeakPower), typeof(VulnerablePower), typeof(DivineRight),
            typeof(XEnergyTestCard), typeof(XStarTestCard),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task XEnergyCost_WithoutTemporaryState_SpendsAllCurrentEnergy()
    {
        Player player = CreatePlayerWithCombatState();
        player.PlayerCombatState!.Energy = 4;
        XEnergyTestCard card = AddToHand<XEnergyTestCard>(player);

        await card.PlayAsync(target: null);

        Assert.Equal(4, card.LastEnergySpent);
        Assert.Equal(4, card.LastEnergyValue);
        Assert.Equal(0, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task XEnergyCost_WithTemporaryOverride_StillSpendsAllCurrentEnergy()
    {
        Player player = CreatePlayerWithCombatState();
        player.PlayerCombatState!.Energy = 4;
        XEnergyTestCard card = AddToHand<XEnergyTestCard>(player);
        card.SetTemporaryCostOverrideThisTurn(2);

        await card.PlayAsync(target: null);

        Assert.Equal(0, card.EnergyCost);
        Assert.Equal(4, card.LastEnergySpent);
        Assert.Equal(4, card.LastEnergyValue);
        Assert.Equal(0, player.PlayerCombatState.Energy);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task XEnergyCost_WithTemporaryFreeState_StillSpendsAllCurrentEnergy(bool freeUntilPlayed)
    {
        Player player = CreatePlayerWithCombatState();
        player.PlayerCombatState!.Energy = 4;
        XEnergyTestCard card = AddToHand<XEnergyTestCard>(player);
        if (freeUntilPlayed)
        {
            card.MakeFreeUntilPlayed();
        }
        else
        {
            card.MakeTemporaryFreeThisTurn();
        }

        await card.PlayAsync(target: null);

        Assert.Equal(0, card.EnergyCost);
        Assert.Equal(4, card.LastEnergySpent);
        Assert.Equal(4, card.LastEnergyValue);
        Assert.Equal(0, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task XStarCost_SpendsAllCurrentStars()
    {
        Player player = CreatePlayerWithCombatState();
        int starsBefore = player.PlayerCombatState!.Stars;
        XStarTestCard card = AddToHand<XStarTestCard>(player);

        await card.PlayAsync(target: null);

        Assert.Equal(starsBefore, card.LastStarsSpent);
        Assert.Equal(0, player.PlayerCombatState.Stars);
    }

    private static Player CreatePlayerWithCombatState()
    {
        var runState = new FakeRunState();
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.ResetCombatState();
        player.Creature.CombatState = new FakeCombatState(runState, player);
        return player;
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);
        return card;
    }

    private sealed class FakeRunState : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public RunRngSet Rng { get; } = new("x-cost-tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            Array.Empty<AbstractModel>();
    }

    private sealed class FakeCombatState(FakeRunState runState, Player player) : ICombatState
    {
        public IRunState RunState => runState;

        public IReadOnlyList<Creature> Allies { get; } = new[] { player.Creature };

        public IReadOnlyList<Creature> Enemies => Array.Empty<Creature>();

        public IReadOnlyList<Creature> Creatures => Allies;

        public IReadOnlyList<Player> Players { get; } = new[] { player };

        public IReadOnlyList<Creature> HittableEnemies => Array.Empty<Creature>();

        public CombatSide CurrentSide { get; set; }

        public int RoundNumber { get; set; } = 1;

        public IEnumerable<AbstractModel> IterateHookListeners() => Array.Empty<AbstractModel>();

        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => Array.Empty<Creature>();

        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) =>
            side == CombatSide.Player ? Allies : Array.Empty<Creature>();

        public bool ContainsCreature(Creature creature) => ReferenceEquals(creature, player.Creature);

        public bool IsLiveCombat() => true;
    }
}

internal abstract class XCostTestCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;
}

internal sealed class XEnergyTestCard : XCostTestCard
{
    public int LastEnergySpent { get; private set; }
    public int LastEnergyValue { get; private set; }

    protected override bool IsXEnergyCost => true;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        LastEnergySpent = cardPlay.Resources.EnergySpent;
        LastEnergyValue = cardPlay.Resources.EnergyValue;
        return Task.CompletedTask;
    }
}

internal sealed class XStarTestCard : XCostTestCard
{
    public int LastStarsSpent { get; private set; }

    protected override bool IsXStarCost => true;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        LastStarsSpent = cardPlay.Resources.StarsSpent;
        return Task.CompletedTask;
    }
}
