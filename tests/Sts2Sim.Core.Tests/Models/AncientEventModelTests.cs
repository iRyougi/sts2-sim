using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

file sealed class AncientTestCharacter : CharacterModel
{
    public override int StartingHp => 10;

    public override int StartingGold => 0;
}

file sealed class AncientTestRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;
}

file sealed class StubAncient : AncientEventModel
{
    public override IReadOnlyList<RelicModel> AllPossibleOptions => Array.Empty<RelicModel>();

    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => new[]
    {
        RelicOption(typeof(AncientTestRelic), "TAKE_RELIC"),
    };
}

file sealed class AncientHookProbe(bool shouldAllow) : AbstractModel
{
    public int InvocationCount { get; private set; }

    public override bool ShouldReceiveCombatHooks => false;

    public override bool ShouldAllowAncient(IRunState runState, Player player, AncientEventModel ancientEvent)
    {
        InvocationCount++;
        return shouldAllow;
    }
}

sealed class AncientHookRelic : RelicModel
{
    public bool ShouldAllow { get; set; }

    public int InvocationCount { get; private set; }

    public override RelicRarity Rarity => RelicRarity.Event;

    public override bool ShouldAllowAncient(IRunState runState, Player player, AncientEventModel ancientEvent)
    {
        InvocationCount++;
        return ShouldAllow;
    }
}

file sealed class AncientHookRunState(params AbstractModel[] listeners) : IRunState
{
    public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
    public RunRngSet Rng { get; } = new("ancient-hook");

    public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

    public IReadOnlyList<Player> Players => Array.Empty<Player>();
    public int TotalFloor => 0;

    public IEnumerable<AbstractModel> IterateHookListeners(Sts2Sim.Core.Combat.ICombatState? childCombatState) => listeners;
}

[Collection("ModelDb")]
public sealed class AncientEventModelTests : IDisposable
{
    public AncientEventModelTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(AncientTestCharacter), typeof(AncientTestRelic), typeof(StubAncient) });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void BeginEvent_HealsOwnerToFull()
    {
        (RunState runState, Player player) = CreateRun();
        player.Creature.LoseHpInternal(player.Creature.MaxHp - 1, default);
        var ancient = (StubAncient)ModelDb.Event<StubAncient>().MutableClone();
        ancient.AssignOwner(player);

        ancient.BeginEvent(runState);

        Assert.Equal(10, player.Creature.CurrentHp);
        Assert.Equal(player.Creature.MaxHp, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task ChooseRelicOption_ObtainsMutableRelicAndFinishesEvent()
    {
        (RunState runState, Player player) = CreateRun();
        var ancient = (StubAncient)ModelDb.Event<StubAncient>().MutableClone();
        ancient.AssignOwner(player);
        ancient.BeginEvent(runState);

        await ancient.ChooseOption(Assert.Single(ancient.CurrentOptions));

        AncientTestRelic relic = Assert.IsType<AncientTestRelic>(Assert.Single(player.Relics));
        Assert.NotSame(ModelDb.Relic<AncientTestRelic>(), relic);
        Assert.True(ancient.IsFinished);
    }

    [Fact]
    public void ShouldAllowAncient_DefaultsTrueButVetoListenerBlocksAfterAllListenersRun()
    {
        var veto = new AncientHookProbe(shouldAllow: false);
        var allow = new AncientHookProbe(shouldAllow: true);
        (_, Player player) = CreateRun();
        var ancient = (StubAncient)ModelDb.Event<StubAncient>().MutableClone();

        bool result = Hook.ShouldAllowAncient(new AncientHookRunState(veto, allow), player, ancient);

        Assert.False(result);
        Assert.Equal(1, allow.InvocationCount);
        Assert.Equal(1, veto.InvocationCount);
    }

    [Fact]
    public async Task EventOption_NullCallbackCreatesLockedNonExecutableOption()
    {
        var option = new EventOption("NOOP", null);
        Assert.True(option.IsLocked);
        Assert.False(option.IsEnabled);
        await Assert.ThrowsAsync<InvalidOperationException>(() => option.Invoke());
    }
    [Fact]
    public async Task BeginEvent_WhenAncientIsVetoed_OffersOnlyFinishProceedOption()
    {
        (RunState runState, Player player) = CreateRun();
        AncientHookRelic veto = AddHookRelic(player, shouldAllow: false);
        AncientHookRelic allow = AddHookRelic(player, shouldAllow: true);
        var ancient = (StubAncient)ModelDb.Event<StubAncient>().MutableClone();
        ancient.AssignOwner(player);

        ancient.BeginEvent(runState);

        EventOption proceed = Assert.Single(ancient.CurrentOptions);
        Assert.Equal("PROCEED", proceed.Key);
        await ancient.ChooseOption(proceed);
        Assert.True(ancient.IsFinished);
        Assert.Equal(1, veto.InvocationCount);
        Assert.Equal(1, allow.InvocationCount);
    }

    private static (RunState RunState, Player Player) CreateRun()
    {
        var runState = new RunState("ancient-event", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<AncientTestCharacter>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static AncientHookRelic AddHookRelic(Player player, bool shouldAllow)
    {
        var relic = (AncientHookRelic)new AncientHookRelic().MutableClone();
        relic.ShouldAllow = shouldAllow;
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
        return relic;
    }
}
