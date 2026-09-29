using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models;

/// <summary>A combat-only orb. Mutable instances belong to one player's orb queue.</summary>
public abstract class OrbModel : AbstractModel
{
    private static readonly ModelId[] ValidOrbIds =
    [
        ModelDb.GetId<LightningOrb>(),
        ModelDb.GetId<FrostOrb>(),
        ModelDb.GetId<DarkOrb>(),
        ModelDb.GetId<PlasmaOrb>(),
        ModelDb.GetId<GlassOrb>(),
    ];

    private Player? _owner;

    public abstract decimal PassiveVal { get; }

    public abstract decimal EvokeVal { get; }

    public bool HasBeenRemovedFromState { get; private set; }

    public Player Owner
    {
        get
        {
            AssertMutable();
            return _owner ?? throw new InvalidOperationException("Orb has no owner.");
        }
        set
        {
            AssertMutable();
            if (_owner is not null && value is not null && !ReferenceEquals(_owner, value))
                throw new InvalidOperationException($"Orb {Id.Entry} already has an owner.");
            _owner = value;
        }
    }

    public ICombatState CombatState => Owner.Creature.CombatState
        ?? throw new InvalidOperationException("Orb owner is not in combat.");

    public override bool ShouldReceiveCombatHooks => true;

    public event Action? PassiveActivated;
    public event Action<IReadOnlyList<Creature>>? EvokeActivated;

    public static OrbModel GetRandomOrb(Rng rng) =>
        ModelDb.GetById<OrbModel>(rng.NextItem(ValidOrbIds)!);

    public OrbModel ToMutable()
    {
        AssertCanonical();
        return (OrbModel)MutableClone();
    }

    protected void ActivatePassive() => PassiveActivated?.Invoke();

    protected void ActivateEvoke(IReadOnlyList<Creature> targets) => EvokeActivated?.Invoke(targets);

    public virtual Task BeforeTurnEndOrbTrigger(ICombatState combatState) => Task.CompletedTask;

    public virtual Task AfterTurnStartOrbTrigger(ICombatState combatState) => Task.CompletedTask;

    public async Task TriggerPassive(ICombatState combatState, Creature? target)
    {
        int count = Hook.ModifyOrbPassiveTriggerCount(combatState, this, 1,
            out IReadOnlyList<AbstractModel> modifiers);
        await Hook.AfterModifyingOrbPassiveTriggerCount(combatState, this, modifiers);
        for (int i = 0; i < count; i++)
            await Passive(combatState, target);
    }

    /// <summary>Direct passive calls intentionally skip trigger-count modification.</summary>
    public virtual Task Passive(ICombatState combatState, Creature? target) => Task.CompletedTask;

    public virtual Task<IReadOnlyList<Creature>> Evoke(ICombatState combatState) =>
        Task.FromResult<IReadOnlyList<Creature>>([]);

    protected decimal ModifyOrbValue(decimal value) => Hook.ModifyOrbValue(CombatState, this, value);

    internal OrbModel CloneForCombat(Player clonedOwner)
    {
        OrbModel clone = (OrbModel)MutableClone();
        clone._owner = clonedOwner;
        return clone;
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        PassiveActivated = null;
        EvokeActivated = null;
    }

    public void RemoveInternal() => HasBeenRemovedFromState = true;
}
