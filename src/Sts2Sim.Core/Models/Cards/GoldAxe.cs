using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>0+1×(本局已出牌总数)伤害。逐字移植公式（<c>MegaCrit.Sts2.Core.Models.Cards.GoldAxe</c>），
/// 偏离 #101：用 <see cref="Entities.Players.PlayerCombatState.CardsPlayedThisCombat"/>（本场战斗内已完成的
/// 出牌总数，不随回合重置，且不含 GoldAxe 自己这次出牌）代替完整战斗历史日志。</summary>
public sealed class GoldAxe : CardModel, ICardChoiceValueProvider, ICardDamageVariableProvider
{
    private decimal _damagePerCardPlayed = 1m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // Native CalculatedDamage.Calculate(null) counts finished plays by every player.
        amount = _damagePerCardPlayed * (CombatState?.Players.Sum(player =>
            player.PlayerCombatState?.CardsPlayedThisCombat ?? 0) ?? 0);
        return true;
    }

    public double CardChoiceValue =>
        (double)(_damagePerCardPlayed * Owner.PlayerCombatState!.CardsPlayedThisCombat);

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    public override bool IsColorless => true;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = _damagePerCardPlayed * Owner.PlayerCombatState!.CardsPlayedThisCombat;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
