using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Orbs;

public sealed class OrbQueue(Player owner)
{
    public const int MaxCapacity = 10;

    private readonly List<OrbModel> _orbs = [];

    public IReadOnlyList<OrbModel> Orbs => _orbs;

    public int Capacity { get; private set; }

    public void Clear()
    {
        _orbs.Clear();
        Capacity = 0;
    }

    public void AddCapacity(int capacity) => Capacity += capacity;

    public void RemoveCapacity(int capacity)
    {
        Capacity = Math.Max(0, Capacity - capacity);
        while (_orbs.Count > Capacity)
            Remove(_orbs[^1]);
    }

    public Task<bool> TryEnqueue(OrbModel orb)
    {
        if (Capacity == 0)
            return Task.FromResult(false);
        orb.AssertMutable();
        if (_orbs.Count >= Capacity)
            throw new InvalidOperationException("OrbQueue is full.");
        _orbs.Add(orb);
        return Task.FromResult(true);
    }

    public bool Remove(OrbModel orb) => _orbs.Remove(orb);

    public void Insert(int index, OrbModel orb)
    {
        if (index >= Capacity)
            throw new InvalidOperationException("Orb index cannot be greater than capacity.");
        _orbs.Insert(index, orb);
    }

    public async Task BeforeTurnEnd(ICombatState combatState)
    {
        foreach (OrbModel orb in _orbs.ToList())
        {
            if (owner.Creature.CombatState is null)
                return;
            await orb.BeforeTurnEndOrbTrigger(combatState);
        }
    }

    public async Task AfterTurnStart(ICombatState combatState)
    {
        foreach (OrbModel orb in _orbs.ToList())
        {
            if (owner.Creature.CombatState is null)
                return;
            await orb.AfterTurnStartOrbTrigger(combatState);
        }
    }

    internal OrbQueue CloneForCombat(Player clonedOwner)
    {
        var clone = new OrbQueue(clonedOwner) { Capacity = Capacity };
        foreach (OrbModel orb in _orbs)
            clone._orbs.Add(orb.CloneForCombat(clonedOwner));
        return clone;
    }
}
