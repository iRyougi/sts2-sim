using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>5+3×(本局生成卡数)伤害。逐字移植公式（<c>MegaCrit.Sts2.Core.Models.Cards.Supermassive</c>），
/// 偏离 #101：用 <see cref="Entities.Players.PlayerCombatState.CardsGeneratedThisCombat"/>（本场战斗内、
/// 由该玩家生成的卡牌数，不随回合重置）代替完整战斗历史日志。</summary>
public sealed class Supermassive : CardModel, ICardChoiceValueProvider, ICardDamageVariableProvider
{
    private const decimal BaseDamage = 5m;
    private decimal _damagePerGeneratedCard = 3m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // Native CalculatedDamage.Calculate(null) counts this owner's generated cards.
        amount = BaseDamage + _damagePerGeneratedCard *
            (CombatState is null ? 0 : Owner.PlayerCombatState!.CardsGeneratedThisCombat);
        return true;
    }

    public double CardChoiceValue => (double)(
        BaseDamage + _damagePerGeneratedCard * Owner.PlayerCombatState!.CardsGeneratedThisCombat);

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal damage = BaseDamage + _damagePerGeneratedCard * Owner.PlayerCombatState!.CardsGeneratedThisCombat;
        await DamageCmd.Attack(damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _damagePerGeneratedCard += 1m;
}
