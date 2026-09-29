using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Squeeze</c>：3 费 Osty 攻击，伤害 25（升级 30）+ 5（升级 6）×战斗中除本牌外所有
/// Osty 攻击牌的张数；Osty 不在时无效果。</summary>
public sealed class Squeeze : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 3;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal CalculationBase => IsUpgraded ? 30m : 25m;

    private decimal ExtraDamage => IsUpgraded ? 6m : 5m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CalculatedDamage();
        return true;
    }

    // 原版 CalculatedVar.Calculate：战斗外乘数为 0；AllCards 是五个战斗牌堆。
    private decimal CalculatedDamage()
    {
        int ostyAttacks = CombatState is not { } combatState || !combatState.IsLiveCombat() ||
            Owner.PlayerCombatState is not { } state
            ? 0
            : state.AllPiles.SelectMany(pile => pile.Cards)
                .Count(card => card.Tags.Contains(CardTag.OstyAttack) && !ReferenceEquals(card, this));
        return CalculationBase + ExtraDamage * ostyAttacks;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(CalculatedDamage()).FromOsty(Owner.Osty!, this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }
}
