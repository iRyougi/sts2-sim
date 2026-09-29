using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Powers;

/// <summary>原版 <c>LethalityPower</c>：持有者本回合第一张攻击牌的强化攻击伤害乘以 (1 + 层数/100)。
/// 原版数本回合该玩家的 <c>CardPlayStartedEntry</c> 中的攻击牌，模拟器用同一时点（BeforeCardPlayed 之后、
/// OnPlay 之前，每次重放各记一次）递增的 <see cref="Entities.Players.PlayerCombatState.AttackCardsStartedThisTurn"/>；
/// 原版的 <c>CurrentPlayIndex</c> 在出牌期间等于当前 <see cref="CardPlay.PlayIndex"/>。</summary>
public sealed class LethalityPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (!props.IsPoweredAttack())
            return 1m;
        if (cardSource is null)
            return 1m;
        if (cardSource.Owner.Creature != Owner)
            return 1m;

        bool inPlayPile = cardSource.Pile?.Type == PileType.Play;
        if (inPlayPile && (cardPlay?.PlayIndex ?? 0) > 0)
            return 1m;

        int attacksStarted = Owner.Player?.PlayerCombatState?.AttackCardsStartedThisTurn ?? 0;
        if (attacksStarted > (inPlayPile ? 1 : 0))
            return 1m;

        return 1m + Amount / 100m;
    }
}
