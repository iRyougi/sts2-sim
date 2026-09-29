namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

public sealed class PossessStrengthPower : PowerModel
{
    private Dictionary<Creature, decimal> _stolen = new();

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Single;

    public override Task AfterPowerAmountChanged(
        PowerModel power,
        decimal amount,
        Creature? applier,
        CardModel? cardSource)
    {
        if (ReferenceEquals(applier, Owner) && power.Owner.IsPlayer && power is StrengthPower && amount < 0)
        {
            _stolen[power.Owner] = _stolen.GetValueOrDefault(power.Owner) + amount;
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDeath(Creature target)
    {
        if (!ReferenceEquals(target, Owner))
        {
            return;
        }

        foreach ((Creature creature, decimal amount) in _stolen)
        {
            await PowerCmd.Apply<StrengthPower>(Owner.CombatState!, creature, -amount, null, null);
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _stolen = new Dictionary<Creature, decimal>();
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        foreach ((Creature creature, decimal amount) in ((PossessStrengthPower)source)._stolen)
        {
            _stolen.Add(creatureMap[creature], amount);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        context.AppendCreatureReferenceValues(ref builder, _stolen);
}
