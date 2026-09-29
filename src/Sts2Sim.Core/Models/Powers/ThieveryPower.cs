namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Players;

public sealed class ThieveryPower : PowerModel
{
    private int _goldStolen;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public Creature? Target { get; set; }

    public int GoldStolen => _goldStolen;

    public async Task Steal()
    {
        if (Target?.Player is not Player player || Target.IsDead || player.Gold <= 0 || Owner.CombatState is null)
        {
            return;
        }

        int stolen = Math.Min(Amount, player.Gold);
        await PlayerCmd.LoseGold(stolen, player, GoldLossType.Stolen);
        _goldStolen = checked(_goldStolen + stolen);
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_goldStolen);
        builder.Append(Target is null ? -1 : context.State.Creatures.ToList().IndexOf(Target));
    }

    internal override void RestoreCombatCloneReferencesFrom(
        PowerModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        base.RestoreCombatCloneReferencesFrom(source, cardMap, creatureMap);
        Target = ((ThieveryPower)source).Target is { } target && creatureMap.TryGetValue(target, out Creature? cloned)
            ? cloned
            : null;
    }
}
