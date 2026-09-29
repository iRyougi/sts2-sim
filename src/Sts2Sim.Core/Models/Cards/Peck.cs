using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Peck : CardModel, ICardChoiceValueProvider, ICardDamageVariableProvider
{
    private int _hitCount = 3;

    public double CardChoiceValue => 2d;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        // RepeatVar changes hit count, not native DamageVar's per-hit value.
        amount = 2m;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Event;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(2m)
            .FromCard(this, cardPlay)
            .WithHitCount(_hitCount)
            .Targeting(cardPlay.Target)
            .Execute();
    }

    protected override void OnUpgrade() => _hitCount++;
}
