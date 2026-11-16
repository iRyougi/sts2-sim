using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Tests.Models.Powers;

[Collection("ModelDb")]
public sealed class ExtraTurnPowerTests : IDisposable
{
    public ExtraTurnPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Metadata_IsBuffCounter()
    {
        ExtraTurnPower power = ModelDb.Power<ExtraTurnPower>();

        Assert.Equal(PowerType.Buff, power.Type);
        Assert.Equal(PowerStackType.Counter, power.StackType);
    }
}
