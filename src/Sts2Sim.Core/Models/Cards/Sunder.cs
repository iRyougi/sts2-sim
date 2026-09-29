using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Sunder : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 26m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage, Energy: 3);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        var attack = await DamageCmd.Attack(_damage).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
        if (attack.Results.SelectMany(hit => hit).Any(result => result.WasTargetKilled))
            await PlayerCmd.GainEnergy(3m, Owner);
    }

    protected override void OnUpgrade() => _damage += 8m;
}
