namespace Sts2Sim.Core.Models.Powers;

using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

public sealed class SurroundedPower : PowerModel
{
    public enum Direction { Left, Right }
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Single;
    public Direction Facing { get; private set; } = Direction.Right;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || dealer is null) return 1m;
        bool backAttack = Facing == Direction.Right && dealer.HasPower<BackAttackLeftPower>() ||
                          Facing == Direction.Left && dealer.HasPower<BackAttackRightPower>();
        return backAttack ? 1.5m : 1m;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player.Creature == Owner && cardPlay.Target is { } target) Face(target);
        return Task.CompletedTask;
    }

    public override Task BeforePotionUsed(PotionModel potion, Creature? target)
    {
        if (potion.Owner.Creature == Owner && target is not null) Face(target);
        return Task.CompletedTask;
    }

    public override Task AfterDeath(Creature target)
    {
        if (target.Side == Owner.Side || Owner.CombatState is null) return Task.CompletedTask;
        Creature[] living = Owner.CombatState.Enemies.Where(enemy => !enemy.IsDead).ToArray();
        if (living.Length == 1) Face(living[0]);
        return Task.CompletedTask;
    }

    private void Face(Creature target)
    {
        if (target.HasPower<BackAttackLeftPower>()) Facing = Direction.Left;
        else if (target.HasPower<BackAttackRightPower>()) Facing = Direction.Right;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append((int)Facing);
}
