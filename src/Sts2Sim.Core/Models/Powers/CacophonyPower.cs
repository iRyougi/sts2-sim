using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>CacophonyPower</c>（每次施加都是独立实例）：任何玩家每抽 1 张牌计数减 1，归零时用
/// <c>CombatTargets</c> 抽一名可命中敌人、计数重置为 33，再对它造成层数的无力伤害。原版在抽目标之后、造成伤害之前
/// 有一次 <c>Cmd.Wait</c>；目标必须在等待前抽定，这里省略等待但保持抽取时机。</summary>
public sealed class CacophonyPower : PowerModel
{
    private const int CardsPerTrigger = 33;

    private int _cardsLeft = CardsPerTrigger;

    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;

    public override async Task AfterCardDrawn(CardModel card, bool fromHandDraw)
    {
        // 原版不检查被抽牌的主人：多人下队友抽牌也计数。
        _cardsLeft--;
        if (_cardsLeft > 0)
            return;

        Creature? enemy = Owner.Player!.RunState.Rng.CombatTargets.NextItem(Owner.CombatState!.HittableEnemies);
        _cardsLeft = CardsPerTrigger;
        if (enemy is not null)
            await CreatureCmd.Damage(Owner.CombatState!, [enemy], Amount, ValueProp.Unpowered, Owner, null, null);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder, CombatStateDescriptionContext context) =>
        builder.Append(_cardsLeft);
}
