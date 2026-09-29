using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Unplayable status that hurts its owner while retained in hand at player turn end.
/// 偏离 #163：真实卡使用专用 OnTurnEndInHand 回调；模拟器改用共享的
/// <see cref="CardModel.HasTurnEndInHandEffect"/>/<see cref="CardModel.OnTurnEndInHand"/> 机制
/// （由 <see cref="Hook.BeforeSideTurnEnd"/> 统一分发）复刻同一触发时机。</summary>
public sealed class Infection : CardModel
{
    public override CardType Type => CardType.Status;

    public override CardRarity Rarity => CardRarity.Status;

    public override TargetType TargetType => TargetType.None;

    public override int MaxUpgradeLevel => 0;

    protected override int CanonicalEnergyCost => -1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords =>
        new[] { CardKeyword.Unplayable };

    protected override bool HasTurnEndInHandEffect => true;

    protected override async Task OnTurnEndInHand()
    {
        ICombatState combatState = CombatState
            ?? throw new InvalidOperationException("Infection turn-end damage requires active combat.");
        await CreatureCmd.Damage(
            combatState,
            new[] { Owner.Creature },
            3m,
            ValueProp.Unpowered | ValueProp.Move,
            dealer: Owner.Creature,
            cardSource: this,
            cardPlay: null);
    }
}
