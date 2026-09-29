using Sts2Sim.Core.Content;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Content.Acts;

public class UnderdocksTests
{
    [Theory]
    [InlineData("underdocks-act-0", "Overgrowth")]
    [InlineData("underdocks-act-1", "Underdocks")]
    public void GetRandomList_RunSeedSelectsBothActOneVariants(string seed, string expectedActName)
    {
        IReadOnlyList<ActDefinition> acts = ActDefinition.GetRandomList(seed);
        var run = new RunState(seed);

        Assert.Equal(expectedActName, acts[0].GetType().Name);
        Assert.Equal(expectedActName, run.Act.GetType().Name);
        Assert.Equal(15, run.Act.BaseNumberOfRooms);
        Assert.Equal(3, run.Act.NumberOfWeakEncounters);
        Assert.Equal(acts.Select(act => act.GetType()), run.Acts.Select(act => act.GetType()));
    }
}
