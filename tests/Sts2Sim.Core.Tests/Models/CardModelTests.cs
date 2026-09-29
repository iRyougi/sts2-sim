namespace Sts2Sim.Core.Tests.Models;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class CardModelTests
{
    public CardModelTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    private sealed class OneCostSkillCard : CardModel
    {
        public bool WasPlayed { get; private set; }

        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 1;

        protected override Task OnPlay(CardPlay cardPlay)
        {
            WasPlayed = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TwoCostPowerCard : CardModel
    {
        public override CardType Type => CardType.Power;

        public override CardRarity Rarity => CardRarity.Uncommon;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 2;

        protected override Task OnPlay(CardPlay cardPlay) => Task.CompletedTask;
    }

    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;

        public RunRngSet Rng { get; } = new RunRngSet("card_model_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState(IRunState runState) : ICombatState
    {
        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);

        public IRunState RunState { get; } = runState;

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
        return player;
    }

    [Fact]
    public void CanPlay_ReturnsFalse_WhenNotEnoughEnergy()
    {
        Player player = MakeInCombatPlayer();
        player.PlayerCombatState!.Energy = 0;
        var card = (OneCostSkillCard)new OneCostSkillCard().MutableClone();
        card.AssignOwner(player);

        bool canPlay = card.CanPlay(out UnplayableReason reason);

        Assert.False(canPlay);
        Assert.Equal(UnplayableReason.EnergyCostTooHigh, reason);
    }

    [Fact]
    public void CanPlay_ReturnsTrue_WhenEnoughEnergy()
    {
        Player player = MakeInCombatPlayer();
        player.PlayerCombatState!.Energy = 3;
        var card = (OneCostSkillCard)new OneCostSkillCard().MutableClone();
        card.AssignOwner(player);

        Assert.True(card.CanPlay(out UnplayableReason reason));
        Assert.Equal(UnplayableReason.None, reason);
    }

    [Fact]
    public async Task PlayAsync_SpendsEnergy_InvokesOnPlay_AndMovesToDiscard()
    {
        Player player = MakeInCombatPlayer();
        player.PlayerCombatState!.Energy = 3;
        var card = (OneCostSkillCard)new OneCostSkillCard().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState.Hand.AddInternal(card);

        await card.PlayAsync(target: null);

        Assert.True(card.WasPlayed);
        Assert.Equal(2, player.PlayerCombatState.Energy);
        Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards);
        Assert.DoesNotContain(card, player.PlayerCombatState.Hand.Cards);
        Assert.Same(player.PlayerCombatState.DiscardPile, card.Pile);
    }

    [Fact]
    public async Task PlayAsync_PowerCard_LeavesCombatEntirely()
    {
        Player player = MakeInCombatPlayer();
        player.PlayerCombatState!.Energy = 3;
        var card = (TwoCostPowerCard)new TwoCostPowerCard().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState.Hand.AddInternal(card);

        await card.PlayAsync(target: null);

        Assert.DoesNotContain(card, player.PlayerCombatState.DiscardPile.Cards);
        Assert.DoesNotContain(card, player.PlayerCombatState.Hand.Cards);
        Assert.DoesNotContain(card, player.PlayerCombatState.PlayPile.Cards);
        Assert.Null(card.Pile);
    }

    [Fact]
    public async Task PlayAsync_NeverSpendsMoreEnergyThanAvailable()
    {
        Player player = MakeInCombatPlayer();
        player.PlayerCombatState!.Energy = 0; // 强制出牌(如自动出牌)场景不应把能量扣成负数——PlayAsync 不自检 CanPlay
        var card = (OneCostSkillCard)new OneCostSkillCard().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState.Hand.AddInternal(card);

        await card.PlayAsync(target: null);

        Assert.True(card.WasPlayed);
        Assert.Equal(0, player.PlayerCombatState.Energy);
        Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards);
    }
}
