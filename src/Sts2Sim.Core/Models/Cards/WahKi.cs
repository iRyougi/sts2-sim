using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>兼带 Wah 与 Ki 标签的两段攻击，下回合获得能量与抽牌（原版 <c>WahKi</c>）。</summary>
public sealed class WahKi : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int Hits = 2;
    private const int Cards = 1;
    private const int Energy = 1;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah, CardTag.Ki];
    private decimal Damage => IsUpgraded ? 4m : 2m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: Cards, Energy: Energy);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).WithHitCount(Hits).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(CombatState!, Owner.Creature, Cards, Owner.Creature, this);
    }
}
