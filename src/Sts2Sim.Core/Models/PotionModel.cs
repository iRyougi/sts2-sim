using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;

namespace Sts2Sim.Core.Models;

/// <summary>Base model for potions, which apply their effects directly when used.</summary>
public abstract class PotionModel : AbstractModel
{
    /// <summary>Rarity used to select potions for shop and reward pools.</summary>
    public abstract PotionRarity Rarity { get; }

    public abstract PotionUsage Usage { get; }

    public abstract TargetType TargetType { get; }

    public virtual bool CanBeGeneratedInCombat => true;

    public virtual bool PassesCustomUsabilityCheck => true;

    public Player Owner { get; private set; } = null!;

    public override bool ShouldReceiveCombatHooks => false;

    public void AssignOwner(Player owner)
    {
        AssertMutable();
        Owner = owner;
    }

    protected abstract Task OnUse(Creature? target);

    public Task UseInternal(Creature? target) => OnUse(target);
}
