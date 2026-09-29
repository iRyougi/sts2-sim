namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

public sealed class HeistPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public Creature? Target { get; set; }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        Target = ((HeistPower)source).Target is { } target && creatureMap.TryGetValue(target, out Creature? cloned)
            ? cloned
            : null;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(Target is null ? -1 : context.State.Creatures.ToList().IndexOf(Target));
    public override Task BeforeDeath(Creature target)
    {
        if (target != Owner || Target?.Player is not Player player ||
            (Owner.CombatState as Combat.CombatState)?.IsProjection == true)
        {
            return Task.CompletedTask;
        }

        if (Owner.CombatState?.RunState.CurrentRoom is CombatRoom room)
        {
            room.AddExtraReward(player, new GoldReward(Amount, player));
        }

        return Task.CompletedTask;
    }
}
