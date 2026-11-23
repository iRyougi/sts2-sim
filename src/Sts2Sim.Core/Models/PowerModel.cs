using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models;

/// <summary>power 基类。偏离 #39：仍未移植多人缩放（ShouldScaleInMultiplayer/GetScaledAmountForMultiplayer）。</summary>
public abstract class PowerModel : AbstractModel, ICombatStateDescriptionContributor
{
    private bool _skipNextDurationTick;
    private int _amountOnTurnStart;

    public abstract PowerType Type { get; }

    public virtual PowerInstanceType InstanceType => PowerInstanceType.None;

    public int Amount { get; private set; }

    /// <summary>Amount captured before any hook for the owner's side starts its turn.</summary>
    public int AmountOnTurnStart
    {
        get => _amountOnTurnStart;
        set
        {
            AssertMutable();
            _amountOnTurnStart = value;
        }
    }

    public abstract PowerStackType StackType { get; }

    public virtual int DisplayAmount => Amount;

    public virtual bool AllowNegative => false;

    public bool SkipNextDurationTick
    {
        get => _skipNextDurationTick;
        set
        {
            AssertMutable();
            _skipNextDurationTick = value;
        }
    }

    public virtual bool OwnerIsSecondaryEnemy => false;

    public PowerType GetTypeForAmount(decimal customAmount)
    {
        if (StackType == PowerStackType.Counter && AllowNegative && customAmount < 0m)
        {
            return PowerType.Debuff;
        }

        if (!AllowNegative && Type == PowerType.Debuff && customAmount < 0m)
        {
            return PowerType.Buff;
        }

        return Type;
    }

    public Creature Owner { get; private set; } = null!;

    public Creature? Applier { get; set; }

    public override bool ShouldReceiveCombatHooks => true;

    public bool ShouldRemoveDueToAmount()
    {
        if (AllowNegative)
        {
            return Amount == 0;
        }
        return Amount <= 0;
    }

    public void SetAmount(int amount)
    {
        AssertMutable();
        Amount = Math.Clamp(amount, -999999999, 999999999);
    }

    public void ApplyInternal(Creature owner, decimal amount)
    {
        AssertMutable();
        Owner = owner;
        SetAmount((int)amount);
        Owner.ApplyPowerInternal(this);
    }

    public void RemoveInternal()
    {
        AssertMutable();
        Owner.RemovePowerInternal(this);
    }

    public virtual Task BeforeApplied(Creature target, decimal amount, Creature? applier, CardModel? cardSource) => Task.CompletedTask;

    public virtual Task AfterApplied(Creature? applier, CardModel? cardSource) => Task.CompletedTask;

    public virtual Task AfterRemoved(Creature oldOwner) => Task.CompletedTask;

    /// <summary>Card references retained by this power even after leaving combat piles.</summary>
    internal virtual IEnumerable<CardModel> EnumerateCombatCloneCards() => [];

    internal virtual void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        Applier = source.Applier is not null ? creatureMap[source.Applier] : null;
    }

    internal virtual void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
    }

    internal void AppendIntrinsicCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        // Central dispatch emits this even when a concrete power reimplements the interface.
        builder.Append(_amountOnTurnStart);
    }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        AppendCombatStateDescription(ref builder, context);
}
