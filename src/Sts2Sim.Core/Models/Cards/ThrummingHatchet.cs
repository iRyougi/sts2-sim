using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>11伤害;上回合打出则本回合自动回手。逐字移植核心行为（<c>MegaCrit.Sts2.Core.Models.Cards.ThrummingHatchet</c>），
/// 偏离 #101：真实源码查战斗历史日志判断"上一个我方回合是否打出过这张具体实例"，本项目没有历史日志系统，
/// 改用"打出时记录当时的 TurnNumber，摸牌前检查 TurnNumber 是否恰好+1"这个等价的轻量判定
/// （开始设置玩家回合时，TurnNumber 会在 <c>Hook.BeforeHandDraw</c> 之前递增，
/// 所以"上一回合打出"精确等价于"当前 TurnNumber == 打出时记录的 TurnNumber + 1"）。</summary>
public sealed class ThrummingHatchet : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 11m;
    private int? _playedOnTurnNumber;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        _playedOnTurnNumber = Owner.PlayerCombatState!.TurnNumber;
    }

    public override Task BeforeHandDraw(Player player)
    {
        if (player == Owner &&
            _playedOnTurnNumber is { } playedOnTurnNumber &&
            Owner.PlayerCombatState!.TurnNumber == playedOnTurnNumber + 1 &&
            Pile?.Type != PileType.Hand)
        {
            CardPileCmd.Add(this, PileType.Hand);
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => _damage += 3m;

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_playedOnTurnNumber.HasValue);
        if (_playedOnTurnNumber.HasValue)
        {
            builder.Append(_playedOnTurnNumber.Value);
        }
    }
}
