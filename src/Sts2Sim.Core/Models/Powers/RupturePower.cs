using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

public sealed class RupturePower : PowerModel
{
    private Dictionary<CardModel, int> _pendingStrength =
        new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner.Creature, Owner) &&
            Owner.CombatState!.CurrentSide == Owner.Side)
            _pendingStrength.Add(cardPlay.Card, 0);
        return Task.CompletedTask;
    }

    public override async Task AfterDamageReceived(
        Creature target, DamageResult result, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (!ReferenceEquals(target, Owner) || result.UnblockedDamage <= 0m ||
            Owner.CombatState!.CurrentSide != Owner.Side)
            return;
        if (cardSource is null || !_pendingStrength.ContainsKey(cardSource))
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState, Owner, Amount, Owner, null);
        else
            _pendingStrength[cardSource] += Amount;
    }

    public override async Task AfterCardPlayed(CardPlay cardPlay)
    {
        if (ReferenceEquals(cardPlay.Card.Owner.Creature, Owner) &&
            _pendingStrength.Remove(cardPlay.Card, out int strength))
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, Owner,
                strength, Owner, null);
    }

    internal override IEnumerable<CardModel> EnumerateCombatCloneCards() =>
        _pendingStrength.Keys;

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _pendingStrength = new Dictionary<CardModel, int>(
            _pendingStrength, ReferenceEqualityComparer.Instance);
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        _pendingStrength = new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
        foreach ((CardModel card, int strength) in ((RupturePower)source)._pendingStrength)
            _pendingStrength.Add(cardMap[card], strength);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        context.AppendCardReferenceValues(ref builder, _pendingStrength);
}
