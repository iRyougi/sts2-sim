using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Entities.Players;

[Collection("ModelDb")]
public class PlayerCombatStateStarsTests : IDisposable
{
    public PlayerCombatStateStarsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    private sealed class StarCostSkillCard : CardModel
    {
        public ResourceInfo? ResourcesAtPlay { get; private set; }

        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 1;

        protected override int CanonicalStarCost => 2;

        protected override Task OnPlay(CardPlay cardPlay)
        {
            ResourcesAtPlay = cardPlay.Resources;
            return Task.CompletedTask;
        }
    }

    private sealed class ZeroStarCostSkillCard : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 1;
    }

    private sealed class StarsSpentSpy : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public int CallCount { get; private set; }

        public int Amount { get; private set; }

        public Player? Spender { get; private set; }

        public override Task AfterStarsSpent(int amount, Player spender)
        {
            CallCount++;
            Amount = amount;
            Spender = spender;
            return Task.CompletedTask;
        }
    }

    private class StarsGainedSpy : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;
        public int CallCount { get; private set; }
        public int Amount { get; private set; }
        public Player? Gainer { get; private set; }
        public override Task AfterStarsGained(int amount, Player gainer)
        {
            CallCount++;
            Amount = amount;
            Gainer = gainer;
            return Task.CompletedTask;
        }
    }

    private sealed class AddingStarsGainedSpy(FakeRunState runState, AbstractModel added) : StarsGainedSpy
    {
        public override Task AfterStarsGained(int amount, Player gainer)
        {
            runState.AddListener(added);
            return base.AfterStarsGained(amount, gainer);
        }
    }

    private sealed class FakeRunState(params AbstractModel[] initialListeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        private readonly List<AbstractModel> _listeners = [.. initialListeners];
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => _listeners;
        public void AddListener(AbstractModel listener) => _listeners.Add(listener);
        public RunRngSet Rng { get; } = new RunRngSet("player_combat_state_stars_tests");
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

    private static Player MakeInCombatPlayer(params AbstractModel[] listeners)
    {
        var runState = new FakeRunState(listeners);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.ResetCombatState();
        player.Creature.CombatState = new FakeCombatState(runState);
        return player;
    }

    [Fact]
    public void GainStars_ThenEnergyReset_DoesNotResetStars()
    {
        Player player = MakeInCombatPlayer();
        PlayerCombatState state = player.PlayerCombatState!;
        state.GainStars(3m);

        state.ResetEnergy();

        Assert.Equal(3, state.Stars);
    }

    [Fact]
    public void LoseStars_ClampsAtZero()
    {
        PlayerCombatState state = MakeInCombatPlayer().PlayerCombatState!;
        state.GainStars(2m);
        state.LoseStars(999m);

        Assert.Equal(0, state.Stars);
    }

    [Fact]
    public void CanPlay_ReturnsStarCostTooHigh_WhenStarsAreInsufficient()
    {
        Player player = MakeInCombatPlayer();
        player.PlayerCombatState!.Energy = 1;
        var card = (StarCostSkillCard)new StarCostSkillCard().MutableClone();
        card.AssignOwner(player);

        bool canPlay = card.CanPlay(out UnplayableReason reason);

        Assert.False(canPlay);
        Assert.Equal(UnplayableReason.StarCostTooHigh, reason);
    }

    [Fact]
    public async Task PlayAsync_SpendsExactStars_ReportsResources_AndInvokesHook()
    {
        var spy = new StarsSpentSpy();
        Player player = MakeInCombatPlayer(spy);
        PlayerCombatState state = player.PlayerCombatState!;
        state.Energy = 1;
        state.GainStars(2m);
        var card = (StarCostSkillCard)new StarCostSkillCard().MutableClone();
        card.AssignOwner(player);
        state.Hand.AddInternal(card);

        await card.PlayAsync(target: null);

        Assert.Equal(0, state.Stars);
        Assert.Equal(new ResourceInfo(1, 1, 2, 2), card.ResourcesAtPlay);
        Assert.Equal(1, spy.CallCount);
        Assert.Equal(2, spy.Amount);
        Assert.Same(player, spy.Spender);
    }

    [Fact]
    public async Task PlayAsync_DoesNotInvokeStarsSpentHook_WhenNoStarsAreSpent()
    {
        var spy = new StarsSpentSpy();
        Player player = MakeInCombatPlayer(spy);
        PlayerCombatState state = player.PlayerCombatState!;
        state.Energy = 1;
        var card = (ZeroStarCostSkillCard)new ZeroStarCostSkillCard().MutableClone();
        card.AssignOwner(player);
        state.Hand.AddInternal(card);

        await card.PlayAsync(target: null);

        Assert.Equal(0, spy.CallCount);
    }

    [Fact]
    public async Task AfterStarsGained_UsesSnapshotOfCombatListeners()
    {
        var runState = new FakeRunState();
        var late = new StarsGainedSpy();
        var first = new AddingStarsGainedSpy(runState, late);
        runState.AddListener(first);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        var combatState = new FakeCombatState(runState);
        player.ResetCombatState();
        player.Creature.CombatState = combatState;
        await Hook.AfterStarsGained(combatState, 1, player);
        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, late.CallCount);
        await Hook.AfterStarsGained(combatState, 1, player);
        Assert.Equal(2, first.CallCount);
        Assert.Equal(1, late.CallCount);
    }

    [Fact]
    public async Task PlayerCmd_GainStars_DispatchesPositiveGainOnly()
    {
        var spy = new StarsGainedSpy();
        Player player = MakeInCombatPlayer(spy);
        await PlayerCmd.GainStars(2m, player);
        await PlayerCmd.GainStars(0m, player);
        Assert.Equal(2, player.PlayerCombatState!.Stars);
        Assert.Equal(1, spy.CallCount);
        Assert.Equal(2, spy.Amount);
        Assert.Same(player, spy.Gainer);
    }

    [Fact]
    public async Task PlayerCmd_GainStars_StillMutatesCombatStateWithoutCreatureCombatState()
    {
        var runState = new FakeRunState();
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.ResetCombatState();

        await PlayerCmd.GainStars(3m, player);

        Assert.Equal(3, player.PlayerCombatState!.Stars);
        Assert.Null(player.Creature.CombatState);
    }

    [Fact]
    public async Task PlayerCmd_GainAndLoseStars_UpdatesCombatState()
    {
        Player player = MakeInCombatPlayer();
        PlayerCombatState state = player.PlayerCombatState!;

        await PlayerCmd.GainStars(3m, player);

        Assert.Equal(3, state.Stars);

        await PlayerCmd.LoseStars(999m, player);

        Assert.Equal(0, state.Stars);
    }
}
