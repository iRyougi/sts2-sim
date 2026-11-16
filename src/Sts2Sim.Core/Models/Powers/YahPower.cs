using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Powers;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>每回合首次拔除己方箭矢时获得能量（原版 <c>YahPower</c>）。</summary>
public sealed class YahPower : PowerModel
{
    private bool _triggeredThisTurn;

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterArrowDislodged(ImpaledPower power, ArrowSlot arrow, CardModel? source)
    {
        if (ReferenceEquals(arrow.Owner.Creature, Owner) && !_triggeredThisTurn)
        {
            _triggeredThisTurn = true;
            await PlayerCmd.GainEnergy(Amount, Owner.Player!);
        }
    }

    public override Task BeforeSideTurnStart(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner))
        {
            _triggeredThisTurn = false;
        }
        return Task.CompletedTask;
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        _triggeredThisTurn = ((YahPower)source)._triggeredThisTurn;
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_triggeredThisTurn);
    }
}
