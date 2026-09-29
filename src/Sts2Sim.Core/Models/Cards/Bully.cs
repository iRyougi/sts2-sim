using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Bully : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = 4m;
        return true;
    }

    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    private decimal _damagePerVulnerable = 2m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        decimal stacks = cardPlay.Target.GetPower<VulnerablePower>()?.Amount ?? 0;
        return DamageCmd.Attack(4m + _damagePerVulnerable * stacks)
            .FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }

    protected override void OnUpgrade() => _damagePerVulnerable++;
}
