using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

public sealed class StrikeIronclad : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 6m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Basic;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new CardChoiceBaseValues(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Strike];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _damage += 3m;
}
