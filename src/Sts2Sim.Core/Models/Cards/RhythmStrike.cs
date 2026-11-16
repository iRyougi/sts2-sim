using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>命中次数等于 1 加本回合旋律种类数的 0 费 Strike（原版 <c>RhythmStrike</c>）。</summary>
public sealed class RhythmStrike : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];
    private decimal Damage => IsUpgraded ? 5m : 4m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = 1 + MelodyCmd.MelodiesThisTurn(Owner);
        await DamageCmd.Attack(Damage).WithHitCount(hits).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
