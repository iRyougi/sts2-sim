using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Models;

public sealed class RelicModelLifecycleTests
{
    private sealed class TestObtainRelic : RelicModel
    {
        public bool WasObtained { get; private set; }
        public override RelicRarity Rarity => RelicRarity.Common;
        public override Task AfterObtained()
        {
            WasObtained = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TestStackableRelic : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.None;
        public override bool IsStackable => true;
    }

    [Fact]
    public async Task RelicCmd_Obtain_CallsAfterObtained()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Sts2Sim.Core.Models.Characters.Regent),
            typeof(Sts2Sim.Core.Models.Cards.StrikeRegent),
            typeof(Sts2Sim.Core.Models.Cards.DefendRegent),
            typeof(Sts2Sim.Core.Models.Cards.FallingStar),
            typeof(Sts2Sim.Core.Models.Cards.Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(TestObtainRelic),
        });
        var runState = new Sts2Sim.Core.Runs.RunState("relic-lifecycle", new Sts2Sim.Core.Content.Acts.Overgrowth());
        Sts2Sim.Core.Entities.Players.Player player =
            Sts2Sim.Core.Entities.Players.Player.CreateForNewRun(ModelDb.Character<Sts2Sim.Core.Models.Characters.Regent>(), runState);

        await RelicCmd.Obtain(ModelDb.Relic<TestObtainRelic>(), player);

        var obtained = player.Relics.OfType<TestObtainRelic>().Single();
        Assert.True(obtained.WasObtained);
        ModelDb.ResetForTests();
    }

    [Fact]
    public void RelicModel_StackCount_IncrementsOnlyWhenStackable()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[] { typeof(TestStackableRelic) });
        var relic = (TestStackableRelic)ModelDb.Relic<TestStackableRelic>().MutableClone();

        Assert.Equal(1, relic.StackCount);
        relic.IncrementStackCount();
        Assert.Equal(2, relic.StackCount);
        ModelDb.ResetForTests();
    }
}
