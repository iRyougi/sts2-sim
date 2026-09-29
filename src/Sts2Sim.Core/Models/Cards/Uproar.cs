using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Helpers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Uproar : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal _damage = 6m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)_damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = _damage; return true; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).FromCard(this, cardPlay).WithHitCount(2)
            .Targeting(cardPlay.Target).Execute();
        CardModel? chosen = Owner.PlayerCombatState!.DrawPile.Cards
            .Where(card => card.Type == CardType.Attack && !card.HasKeyword(CardKeyword.Unplayable))
            .ToList().StableShuffle(Owner.RunState.Rng.Shuffle).FirstOrDefault();
        if (chosen is null)
            chosen = Owner.PlayerCombatState.DrawPile.Cards.Where(card => card.Type == CardType.Attack)
                .ToList().StableShuffle(Owner.RunState.Rng.Shuffle).FirstOrDefault();
        if (chosen is not null) await AutoPlayCmd.FromCards(CombatState!, Owner, [chosen]);
    }

    protected override void OnUpgrade() => _damage += 2m;
}
