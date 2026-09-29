using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>BlightStrike</c>：1 费攻击，造成 8（升级 10），再给目标施加等于本次攻击总伤害（含被格挡部分）的灾厄。</summary>
public sealed class BlightStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Common;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];

    private decimal Damage => IsUpgraded ? 10m : 8m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var attack = await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        int totalDamage = attack.Results.SelectMany(results => results).Sum(result => result.TotalDamage);
        await PowerCmd.Apply<DoomPower>(CombatState!, cardPlay.Target, totalDamage, Owner.Creature, this);
    }
}
