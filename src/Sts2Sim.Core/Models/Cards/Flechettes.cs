using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>Authoritative Silent Flechettes: one hit per Skill currently in hand.</summary>
public sealed class Flechettes : CardModel, ICardDamageVariableProvider
{
    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    private decimal _damage = 5m;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int hits = Owner.PlayerCombatState!.Hand.Cards.Count(card => card.Type == CardType.Skill);
        await DamageCmd.Attack(_damage).WithHitCount(hits).FromCard(this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }
    protected override void OnUpgrade() => _damage += 2m;
}
