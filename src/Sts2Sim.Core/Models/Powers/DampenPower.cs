namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Combat.StateDescription;

public sealed class DampenPower : PowerModel
{
    private HashSet<Creature> _casters = new();
    private Dictionary<CardModel, int> _downgraded = new(ReferenceEqualityComparer.Instance);

    public override PowerType Type => PowerType.Debuff;

    public override PowerStackType StackType => PowerStackType.None;

    public void AddCaster(Creature caster) => _casters.Add(caster);

    public override Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        foreach (CardModel card in Owner.Player!.PlayerCombatState!.AllPiles
                     .SelectMany(pile => pile.Cards)
                     .Where(card => card.IsUpgraded))
        {
            _downgraded[card] = card.CurrentUpgradeLevel;
            card.Downgrade();
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDeath(Creature target)
    {
        if (!_casters.Remove(target) || _casters.Count > 0)
        {
            return;
        }

        await PowerCmd.Remove(this);
    }

    public override Task AfterRemoved(Creature oldOwner)
    {
        foreach ((CardModel card, int level) in _downgraded)
        {
            while (card.CurrentUpgradeLevel < level)
            {
                CardCmd.Upgrade(card);
            }
        }

        _downgraded.Clear();
        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _casters = new HashSet<Creature>();
        _downgraded = new Dictionary<CardModel, int>(ReferenceEqualityComparer.Instance);
    }

    internal override IEnumerable<CardModel> EnumerateCombatCloneCards() => _downgraded.Keys;

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        var sourcePower = (DampenPower)source;
        foreach (Creature caster in sourcePower._casters)
        {
            _casters.Add(creatureMap[caster]);
        }

        foreach ((CardModel card, int level) in sourcePower._downgraded)
        {
            _downgraded.Add(cardMap[card], level);
        }
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        context.AppendCreatureReferences(ref builder, _casters);
        context.AppendCardReferenceValues(ref builder, _downgraded);
    }
}
