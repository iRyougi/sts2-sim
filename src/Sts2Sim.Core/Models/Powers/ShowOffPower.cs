using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models.Cards;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>本回合有敌方触发致命死亡时提升绑定 ShowOff 实例的 Aim 并自我移除（原版 <c>ShowOffPower</c>）。</summary>
public sealed class ShowOffPower : PowerModel
{
    private ShowOff? _showOffInstance;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public ShowOff? ShowOffInstance
    {
        get => _showOffInstance;
        set
        {
            AssertMutable();
            _showOffInstance = value;
        }
    }

    public override async Task AfterDeath(Creature target, CardModel? source, bool wasRemovalPrevented)
    {
        if (target.Side != Owner.Side && target.Powers.All(p => p.ShouldOwnerDeathTriggerFatal()))
        {
            _showOffInstance?.IncreaseAim(Amount);
            await PowerCmd.Remove(this);
        }
    }

    public override async Task AfterSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            await PowerCmd.Remove(this);
        }
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        ShowOff? sourceCard = ((ShowOffPower)source)._showOffInstance;
        _showOffInstance = sourceCard is not null && cardMap.TryGetValue(sourceCard, out CardModel? mapped)
            ? (ShowOff)mapped
            : null;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        context.AppendCardReferences(ref builder, [_showOffInstance]);
}
