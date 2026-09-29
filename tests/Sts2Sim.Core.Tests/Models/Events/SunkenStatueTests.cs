using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Events;

[Collection("ModelDb")]
public sealed class SunkenStatueTests : IDisposable
{
    public SunkenStatueTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(DivineRight),
            typeof(SwordOfStone),
            typeof(SwordOfJade),
            typeof(TungstenRod),
            typeof(SunkenStatue),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task GrabSword_ObtainsASwordOfStoneThroughTheNormalRelicLifecycle()
    {
        (_, Player player, SunkenStatue ev) = Setup("sunken-sword");

        await Choose(ev, "GRAB_SWORD");

        Assert.Single(player.Relics.OfType<SwordOfStone>());
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task DiveIntoWater_GainsGoldInInclusiveRange_ThenDealsSevenUnblockableDamage()
    {
        (_, Player player, SunkenStatue ev) = Setup("sunken-dive");
        int goldBefore = player.Gold;
        int hpBefore = player.Creature.CurrentHp;
        player.Creature.GainBlockInternal(20m);

        await Choose(ev, "DIVE_INTO_WATER");

        Assert.InRange(player.Gold - goldBefore, 101, 121);
        Assert.Equal(hpBefore - 7, player.Creature.CurrentHp);
        Assert.Equal(20, player.Creature.Block);
        Assert.True(ev.IsFinished);
    }

    [Fact]
    public async Task DiveIntoWater_TungstenRodReducesHpLossToSixWithoutConsumingBlock()
    {
        (_, Player player, SunkenStatue ev) = Setup("sunken-tungsten");
        await RelicCmd.Obtain(ModelDb.Relic<TungstenRod>(), player);
        int hpBefore = player.Creature.CurrentHp;
        player.Creature.GainBlockInternal(20m);

        await Choose(ev, "DIVE_INTO_WATER");

        Assert.Equal(hpBefore - 6, player.Creature.CurrentHp);
        Assert.Equal(20, player.Creature.Block);
    }

    [Fact]
    public async Task DiveIntoWater_DeterministicEventRngCanReachBothInclusiveGoldEndpoints()
    {
        var observed = new HashSet<int>();
        for (int i = 0; i < 2_048 && (!observed.Contains(101) || !observed.Contains(121)); i++)
        {
            (_, Player player, SunkenStatue ev) = Setup($"sunken-endpoint-{i}");
            int goldBefore = player.Gold;

            await Choose(ev, "DIVE_INTO_WATER");

            observed.Add(player.Gold - goldBefore);
        }

        Assert.Contains(101, observed);
        Assert.Contains(121, observed);
    }

    private static (RunState RunState, Player Player, SunkenStatue Event) Setup(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var ev = (SunkenStatue)ModelDb.Event<SunkenStatue>().MutableClone();
        ev.AssignOwner(player);
        ev.BeginEvent(runState);
        return (runState, player, ev);
    }

    private static Task Choose(SunkenStatue ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
