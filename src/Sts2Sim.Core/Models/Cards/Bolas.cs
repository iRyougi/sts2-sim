using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>3伤害;上回合打出则本回合开局自动回手。逐字移植核心行为（<c>MegaCrit.Sts2.Core.Models.Cards.Bolas</c>），
/// 偏离 #101：和 <see cref="ThrummingHatchet"/> 一样，用"记录出牌时的 TurnNumber"代替战斗历史日志。</summary>
public sealed class Bolas : CardModel, ICombatStateDescriptionContributor, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 3m;
    private int? _playedOnTurnNumber;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 0;

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

    protected override void OnUpgrade() => _damage += 1m;

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_playedOnTurnNumber.HasValue);
        if (_playedOnTurnNumber.HasValue)
        {
            builder.Append(_playedOnTurnNumber.Value);
        }
    }
}
