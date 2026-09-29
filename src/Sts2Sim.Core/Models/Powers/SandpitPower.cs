namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;

public sealed class SandpitPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    public Creature? Target { get; set; }

    public override async Task AfterSideTurnStartLate(CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (side != CombatSide.Enemy || !Owner.Powers.Contains(this)) return;
        await PowerCmd.ModifyAmount(Owner.CombatState!, this, -1m, null, null);
        if (!Owner.Powers.Contains(this) && !Owner.IsDead && Target is { IsDead: false })
        {
            await CreatureCmd.Kill(Target);
        }
    }

    internal override void RestoreCombatCloneReferencesFrom(PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        Target = ((SandpitPower)source).Target is { } target ? creatureMap[target] : null;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(Target?.CombatId);
}
