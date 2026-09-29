using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>基础攻击卡。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.StrikeRegent</c>）。</summary>
public sealed class StrikeRegent : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 6m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Basic;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => new[] { CardTag.Strike };

    protected override void OnUpgrade() => _damage += 3m;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
