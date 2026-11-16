using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>射击造成伤害并按总伤害与溢出伤害之和施加 Stretch（原版 <c>BlossomShot</c>）。</summary>
public sealed class BlossomShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    private decimal Damage => IsUpgraded ? 14m : 11m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (cardPlay.Target.IsDead)
            return;

        await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, this, async () =>
        {
            AttackCommand executed = await DamageCmd.Attack(Damage)
                .FromCard(this, cardPlay)
                .FromArrow()
                .Targeting(cardPlay.Target)
                .Execute();
            int total = executed.Results.SelectMany(r => r).Sum(r => r.TotalDamage + r.OverkillDamage);
            await PowerCmd.Apply<StretchPower>(CombatState!, Owner.Creature, total, Owner.Creature, this);
        });
    }
}
