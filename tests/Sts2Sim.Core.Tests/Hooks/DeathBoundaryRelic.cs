using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Tests.Hooks;

public sealed class DeathBoundaryRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public List<string> Trace { get; } = [];
    public bool HealOnPrevention { get; set; } = true;
    public bool AlwaysPrevent { get; set; }
    private bool _prevented;
    public override Task BeforeDeath(Creature creature)
    {
        if (creature == Owner.Creature) Trace.Add("before");
        return Task.CompletedTask;
    }    public override bool ShouldDie(Creature creature)
    {
        if (creature != Owner.Creature) return true;
        Trace.Add("should");
        if (_prevented && !AlwaysPrevent) return true;
        _prevented = true;
        return false;
    }
    public override Task AfterDeath(Creature creature, bool wasRemovalPrevented)
    {
        if (creature == Owner.Creature) Trace.Add($"death:{wasRemovalPrevented}:{creature.CurrentHp}");
        return Task.CompletedTask;
    }
    public override Task AfterPreventingDeath(Creature creature)
    {
        Trace.Add("prevent");
        if (HealOnPrevention) creature.HealInternal(10);
        return Task.CompletedTask;
    }
}
