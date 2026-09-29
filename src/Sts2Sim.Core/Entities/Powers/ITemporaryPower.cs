using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Powers;

/// <summary>Marks a power whose gameplay effect is restored or removed by its own lifecycle hook.</summary>
public interface ITemporaryPower
{
    PowerModel InternallyAppliedPower { get; }
}
