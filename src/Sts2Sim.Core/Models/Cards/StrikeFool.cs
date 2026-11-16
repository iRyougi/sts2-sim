using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Fool 基础攻击（原版 <c>MegaCrit.Sts2.Core.Models.Cards.StrikeFool</c>）。</summary>
public sealed class StrikeFool : CardModel, ICardChoiceBaseValueProvider
{
    private decimal _damage = 6m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Basic;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _damage += 3m;
}
