using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>箭矢穿刺状态（原版 <c>MegaCrit.Sts2.Core.Models.Powers.ImpaledPower</c>，按施放者实例化）。</summary>
public sealed class ImpaledPower : PowerModel
{
    private List<ArrowSlot> _arrowSources = new();
    private int _hitCounter = 3;

    public override PowerType Type => PowerType.None;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.InstancedPerApplier;
    public override int DisplayAmount => _hitCounter;

    public IReadOnlyList<ArrowSlot> Arrows => _arrowSources.ToList();

    public int HitCounter
    {
        get => _hitCounter;
        set
        {
            AssertMutable();
            _hitCounter = value;
        }
    }

    public void AddArrow(ArrowSlot arrow)
    {
        AssertMutable();
        _arrowSources.Add(arrow);
    }

    public ArrowSlot RemoveFirstArrow()
    {
        AssertMutable();
        if (_arrowSources.Count == 0)
            throw new InvalidOperationException("There are no arrows to remove.");
        ArrowSlot arrow = _arrowSources[0];
        _arrowSources.RemoveAt(0);
        return arrow;
    }

    public async Task RemoveIfNoArrows()
    {
        if (_arrowSources.Count <= 0 && Owner.Powers.Contains(this))
        {
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterDamageReceived(
        Creature target,
        DamageResult _,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner && dealer is not null && dealer == Applier && props.IsPoweredAttack())
        {
            HitCounter--;
            if (HitCounter <= 0)
            {
                HitCounter = 3;
                await ArrowCmd.DislodgeArrow(Owner.CombatState!, this, cardSource);
                await RemoveIfNoArrows();
            }
        }
    }

    public override async Task AfterDeath(Creature target, CardModel? source, bool wasRemovalPrevented)
    {
        if (target == Owner && Applier is { IsDead: false })
        {
            await DislodgeAllArrows(source);
            await PowerCmd.Remove(this);
        }
    }

    public async Task DislodgeAllArrows(CardModel? source)
    {
        while (_arrowSources.Count > 0)
        {
            await ArrowCmd.DislodgeArrow(Owner.CombatState!, this, source);
        }
    }

    public async Task ForceRemoveArrow(ArrowSlot arrow)
    {
        _arrowSources.Remove(arrow);
        await RemoveIfNoArrows();
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _arrowSources = new List<ArrowSlot>();
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var sourcePower = (ImpaledPower)source;
        _hitCounter = sourcePower._hitCounter;
        _arrowSources = new List<ArrowSlot>(sourcePower._arrowSources.Count);
        foreach (ArrowSlot sourceSlot in sourcePower._arrowSources)
        {
            Player? sourcePlayer = sourceSlot.Owner;
            int index = -1;
            if (sourcePlayer?.PlayerCombatState is { } sourcePcs)
            {
                for (int i = 0; i < sourcePcs.Arrows.Count; i++)
                {
                    if (ReferenceEquals(sourcePcs.Arrows[i], sourceSlot))
                    {
                        index = i;
                        break;
                    }
                }
            }
            if (index < 0 || sourcePlayer is null || !creatureMap.TryGetValue(sourcePlayer.Creature, out Creature? clonedCreature) ||
                clonedCreature.Player?.PlayerCombatState is not { } clonedPcs ||
                index >= clonedPcs.Arrows.Count)
            {
                throw new InvalidOperationException("Cannot map ImpaledPower arrow slot to cloned player.");
            }
            _arrowSources.Add(clonedPcs.Arrows[index]);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_hitCounter);
        builder.Append(_arrowSources.Count);
        foreach (ArrowSlot slot in _arrowSources)
        {
            Player? slotOwner = slot.Owner;
            int ownerIndex = slotOwner is null ? -1 : context.State.Players.ToList().IndexOf(slotOwner);
            int slotIndex = -1;
            if (slotOwner?.PlayerCombatState is { } pcs)
            {
                for (int i = 0; i < pcs.Arrows.Count; i++)
                {
                    if (ReferenceEquals(pcs.Arrows[i], slot))
                    {
                        slotIndex = i;
                        break;
                    }
                }
            }
            builder.Append(ownerIndex);
            builder.Append(slotIndex);
        }
    }
}
