using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models;

public abstract class RunModel : AbstractModel
{
    private IRunState? _runState;

    public override bool ShouldReceiveCombatHooks => true;

    public IRunState RunState
    {
        get
        {
            AssertMutable();
            return _runState!;
        }
        internal set
        {
            AssertMutable();
            _runState = value;
        }
    }

    /// <summary>Native <c>RunModel.AfterRunLoaded</c> (invoked by <c>OnRunLoaded</c>).</summary>
    public virtual Task AfterRunLoaded() => Task.CompletedTask;

    protected override void AfterCloned()
    {
        base.AfterCloned();
        _runState = null;
    }

    internal RunModel CloneForRun(IRunState targetRun)
    {
        var clone = (RunModel)MutableClone();
        clone.RunState = targetRun;
        return clone;
    }
}
