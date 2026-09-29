using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Powers;

public sealed class SwordSagePower : PowerModel
{
    private Dictionary<CardModel, int> _grantedReplays =
        new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (!ReferenceEquals(power, this))
        {
            return Task.CompletedTask;
        }

        foreach (SovereignBlade blade in Owner.Player!.PlayerCombatState!.AllPiles
                     .SelectMany(pile => pile.Cards)
                     .OfType<SovereignBlade>())
        {
            AddGrant(blade, (int)amount);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (card.Owner == Owner.Player && card is SovereignBlade blade)
        {
            AddGrant(blade, Amount);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardOwnerChanged(CardModel card, Player oldOwner)
    {
        if (card is not SovereignBlade blade)
        {
            return Task.CompletedTask;
        }

        if (oldOwner == Owner.Player && card.Owner != Owner.Player)
        {
            RemoveGrant(blade);
        }
        else if (oldOwner != Owner.Player && card.Owner == Owner.Player)
        {
            AddGrant(blade, Amount);
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEntryAborted(CardModel card)
    {
        if (_grantedReplays.Remove(card, out int granted))
        {
            card.BaseReplayCount -= granted;
        }

        return Task.CompletedTask;
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach ((CardModel card, int granted) in _grantedReplays)
        {
            card.BaseReplayCount -= granted;
        }
        _grantedReplays.Clear();
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _grantedReplays = new Dictionary<CardModel, int>(
            ReferenceEqualityComparer.Instance);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        foreach ((CardModel card, int granted) in
                 ((SwordSagePower)source)._grantedReplays)
        {
            if (!cardMap.TryGetValue(card, out CardModel? clone))
            {
                throw new InvalidOperationException(
                    "Sword Sage tracked a card outside the combat clone graph.");
            }

            _grantedReplays.Add(clone, granted);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AppendCardReferenceValues(ref builder, _grantedReplays);
    }

    private void AddGrant(SovereignBlade blade, int amount)
    {
        if (amount == 0)
        {
            return;
        }

        blade.BaseReplayCount += amount;
        int updated = _grantedReplays.GetValueOrDefault(blade) + amount;
        if (updated == 0)
        {
            _grantedReplays.Remove(blade);
        }
        else
        {
            _grantedReplays[blade] = updated;
        }
    }

    private void RemoveGrant(SovereignBlade blade)
    {
        if (_grantedReplays.Remove(blade, out int granted))
        {
            blade.BaseReplayCount -= granted;
        }
    }
}
