using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>TheScythe</c>：2 费、消耗，造成 13 + 累计增幅 的伤害；打出后本牌与其牌组原件的
/// 增幅都加 5（升级 7，取打出这张的数值）。原版把 <c>CurrentDamage</c>/<c>IncreasedDamage</c> 存档，
/// 这里只保留增幅，伤害恒为 13 + 增幅（与原版 UpdateDamage/AfterDowngraded 的结果一致）。</summary>
public sealed class TheScythe : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int BaseDamage = 13;

    // 跨战斗累积：战斗副本通过 MutableClone 继承牌组原件的值，打出时两边同时增加。
    private int _increasedDamage;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 2;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    public int IncreasedDamage => _increasedDamage;

    private int CurrentDamage => BaseDamage + _increasedDamage;

    private int Increase => IsUpgraded ? 7 : 5;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: CurrentDamage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = CurrentDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(CurrentDamage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        int increase = Increase;
        BuffFromPlay(increase);
        (DeckVersion as TheScythe)?.BuffFromPlay(increase);
    }

    private void BuffFromPlay(int extraDamage)
    {
        AssertMutable();
        _increasedDamage = checked(_increasedDamage + extraDamage);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(_increasedDamage);
}
