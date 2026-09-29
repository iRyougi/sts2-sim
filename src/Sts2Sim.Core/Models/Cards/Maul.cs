using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Maul : CardModel, ICombatStateDescriptionContributor, ICardDamageVariableProvider
{
    private decimal _damage = 5m;
    private decimal _increase = 2m;
    private decimal _extraDamageFromMaulPlays;

    public decimal ExtraDamageFromMaulPlays => _extraDamageFromMaulPlays;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Ancient;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(_damage).WithHitCount(2).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        foreach (Maul maul in Owner.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).OfType<Maul>())
        {
            maul.BuffFromMaulPlay(_increase);
        }
    }

    protected override void OnUpgrade()
    {
        _damage += 1m;
        _increase += 1m;
    }

    private void BuffFromMaulPlay(decimal extraDamage)
    {
        _damage += extraDamage;
        _extraDamageFromMaulPlays += extraDamage;
    }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        builder.Append(_damage);
        builder.Append(_increase);
        builder.Append(_extraDamageFromMaulPlays);
    }
}
