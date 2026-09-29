namespace Sts2Sim.Core.Tests.Hooks;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public class TurnEnergyDrawHookTests
{
    public TurnEnergyDrawHookTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(Sts2Sim.Core.Models.Cards.FallingStar), typeof(Sts2Sim.Core.Models.Cards.Venerate), typeof(Sts2Sim.Core.Models.Powers.WeakPower), typeof(Sts2Sim.Core.Models.Powers.VulnerablePower), typeof(Sts2Sim.Core.Models.Relics.DivineRight) });
    }

    private sealed class FakeRunState(params AbstractModel[] listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;

        public RunRngSet Rng { get; } = new RunRngSet("turn_energy_draw_hook_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class FakeCombatState : ICombatState
    {
        public FakeCombatState(IRunState runState)
        {
            RunState = runState;
        }

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

    private sealed class MutableFakeRunState(List<AbstractModel> listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;

        public RunRngSet Rng { get; } = new RunRngSet("mutable_hook_listener_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;
    }

    private sealed class EnergySpentProbe : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public int CallCount { get; private set; }

        public Action? OnCalled { get; init; }

        public override Task AfterEnergySpent(CardModel card, int amount)
        {
            CallCount++;
            OnCalled?.Invoke();
            return Task.CompletedTask;
        }
    }

    private sealed class TurnStartPassProbe(string name, List<string> log) : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public override Task AfterSideTurnStart(
            CombatSide side,
            IReadOnlyList<Creature> participants)
        {
            log.Add($"{name}:normal");
            return Task.CompletedTask;
        }

        public override Task AfterSideTurnStartLate(
            CombatSide side,
            IReadOnlyList<Creature> participants)
        {
            log.Add($"{name}:late");
            return Task.CompletedTask;
        }
    }

    private sealed class ProbeModel : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public string Name { get; init; } = "probe";

        public List<string>? SharedLog { get; init; }

        public bool AllowReset { get; init; } = true;

        public Func<decimal, decimal>? OnModifyMaxEnergy { get; init; }

        public Func<decimal, decimal>? OnModifyHandDraw { get; init; }

        private void Log() => SharedLog?.Add(Name);

        public override Task BeforeCombatStart()
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task AfterSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task AfterEnergyReset(Player player)
        {
            Log();
            return Task.CompletedTask;
        }

        public override bool ShouldPlayerResetEnergy(Player player)
        {
            Log();
            return AllowReset;
        }

        public override decimal ModifyMaxEnergy(Player player, decimal amount)
        {
            return OnModifyMaxEnergy?.Invoke(amount) ?? amount;
        }

        public override Task BeforeHandDraw(Player player)
        {
            Log();
            return Task.CompletedTask;
        }

        public override decimal ModifyHandDraw(Player player, decimal originalCardCount)
        {
            return OnModifyHandDraw?.Invoke(originalCardCount) ?? originalCardCount;
        }

        public override Task AfterCardDrawn(CardModel card, bool fromHandDraw)
        {
            SharedLog?.Add($"{Name}:{fromHandDraw}");
            return Task.CompletedTask;
        }

        public override Task BeforeCardPlayed(CardPlay cardPlay)
        {
            Log();
            return Task.CompletedTask;
        }

        public override Task AfterCardPlayed(CardPlay cardPlay)
        {
            Log();
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task NotifyHooks_FireInListenerOrder()
    {
        var log = new List<string>();
        var a = new ProbeModel { Name = "a", SharedLog = log };
        var b = new ProbeModel { Name = "b", SharedLog = log };
        var combatState = new FakeCombatState(new FakeRunState(a, b));

        await Hook.BeforeCombatStart(combatState);
        await Hook.BeforeSideTurnStart(combatState, CombatSide.Player, Array.Empty<Creature>());
        await Hook.AfterSideTurnStart(combatState, CombatSide.Player, Array.Empty<Creature>());
        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Player, Array.Empty<Creature>());
        await Hook.AfterSideTurnEnd(combatState, CombatSide.Player, Array.Empty<Creature>());

        Assert.Equal(new[] { "a", "b", "a", "b", "a", "b", "a", "b", "a", "b" }, log);
    }

    [Fact]
    public async Task AfterSideTurnStart_CompletesNormalPassBeforeLatePass()
    {
        var log = new List<string>();
        var a = new TurnStartPassProbe("a", log);
        var b = new TurnStartPassProbe("b", log);
        var combatState = new FakeCombatState(new FakeRunState(a, b));

        await Hook.AfterSideTurnStart(combatState, CombatSide.Enemy, Array.Empty<Creature>());

        Assert.Equal(
            new[] { "a:normal", "b:normal", "a:late", "b:late" },
            log);
    }

    [Fact]
    public async Task AfterEnergyReset_And_BeforeHandDraw_NotifyAllListeners()
    {
        var log = new List<string>();
        var a = new ProbeModel { Name = "a", SharedLog = log };
        var combatState = new FakeCombatState(new FakeRunState(a));
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), combatState.RunState);

        await Hook.AfterEnergyReset(combatState, player);
        await Hook.BeforeHandDraw(combatState, player);

        Assert.Equal(new[] { "a", "a" }, log);
    }

    [Fact]
    public void ShouldPlayerResetEnergy_AndFold_ShortCircuitsOnFalse()
    {
        var log = new List<string>();
        var allow = new ProbeModel { Name = "allow", AllowReset = true, SharedLog = log };
        var veto = new ProbeModel { Name = "veto", AllowReset = false, SharedLog = log };
        var after = new ProbeModel { Name = "after", AllowReset = true, SharedLog = log };
        var combatState = new FakeCombatState(new FakeRunState(allow, veto, after));
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), combatState.RunState);

        bool result = Hook.ShouldPlayerResetEnergy(combatState, player);

        Assert.False(result);
        Assert.Equal(new[] { "allow", "veto" }, log);
    }

    [Fact]
    public void ModifyMaxEnergy_ChainsThroughListenersInOrder()
    {
        var doubler = new ProbeModel { OnModifyMaxEnergy = v => v * 2m };
        var addOne = new ProbeModel { OnModifyMaxEnergy = v => v + 1m };
        var combatState = new FakeCombatState(new FakeRunState(doubler, addOne));
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), combatState.RunState);

        decimal result = Hook.ModifyMaxEnergy(combatState, player, 3m);

        Assert.Equal(7m, result);
    }

    [Fact]
    public void ModifyHandDraw_ChainsThroughListenersInOrder()
    {
        var addTwo = new ProbeModel { OnModifyHandDraw = v => v + 2m };
        var combatState = new FakeCombatState(new FakeRunState(addTwo));
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), combatState.RunState);

        decimal result = Hook.ModifyHandDraw(combatState, player, 5m);

        Assert.Equal(7m, result);
    }

    [Fact]
    public async Task AfterCardDrawn_PassesFromHandDrawFlagThrough()
    {
        var log = new List<string>();
        var a = new ProbeModel { Name = "a", SharedLog = log };
        var combatState = new FakeCombatState(new FakeRunState(a));
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), combatState.RunState);
        CardModel card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);

        await Hook.AfterCardDrawn(combatState, card, fromHandDraw: false);

        Assert.Equal(new[] { "a:False" }, log);
    }

    [Fact]
    public async Task AfterEnergySpent_UsesSnapshot_WhenListenerAddsAnotherListener()
    {
        var listeners = new List<AbstractModel>();
        var late = new EnergySpentProbe();
        var first = new EnergySpentProbe { OnCalled = () => listeners.Add(late) };
        listeners.Add(first);
        var combatState = new FakeCombatState(new MutableFakeRunState(listeners));
        CardModel card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();

        await Hook.AfterEnergySpent(combatState, card, 1);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, late.CallCount);
    }
    [Fact]
    public async Task BeforeCardPlayed_AfterCardPlayed_NotifyWithSameCardPlayInstance()
    {
        var log = new List<string>();
        var a = new ProbeModel { Name = "a", SharedLog = log };
        var combatState = new FakeCombatState(new FakeRunState(a));
        var player = Player.CreateForNewRun(ModelDb.Character<Regent>(), combatState.RunState);
        CardModel card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);
        var cardPlay = new CardPlay
        {
            Card = card,
            Player = player,
            Target = null,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo(1, 1, 0, 0),
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 1,
        };

        await Hook.BeforeCardPlayed(combatState, cardPlay);
        await Hook.AfterCardPlayed(combatState, cardPlay);

        Assert.Equal(new[] { "a", "a" }, log);
    }
}
