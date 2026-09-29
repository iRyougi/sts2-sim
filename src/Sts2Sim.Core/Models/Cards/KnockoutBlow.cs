using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>30伤害,击杀则获5星愿。逐字移植（<c>MegaCrit.Sts2.Core.Models.Cards.KnockoutBlow</c>）。</summary>
public sealed class KnockoutBlow : CardModel, ICardDamageVariableProvider
{
    private decimal _damage = 30m;
    private const decimal StarsOnKill = 5m;

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = _damage;
        return true;
    }

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 3;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand attack = await DamageCmd.Attack(_damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (attack.Results.SelectMany(hits => hits).Any((DamageResult r) => r.WasTargetKilled))
        {
            await PlayerCmd.GainStars(StarsOnKill, Owner);
        }
    }

    protected override void OnUpgrade() => _damage += 8m;
}
