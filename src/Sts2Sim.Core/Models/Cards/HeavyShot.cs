using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.FoolArrows;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>双倍受 AimPower 加成的射击攻击（原版 <c>HeavyShot</c>）。</summary>
public sealed class HeavyShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider, IAimAmountModifier
{
    private const decimal AimMultiplier = 2m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];
    private decimal Damage => IsUpgraded ? 25m : 20m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    public decimal ModifyAimAmount(decimal originalAmount) => originalAmount * AimMultiplier;

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand command = DamageCmd.Attack(Damage).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
        await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, command);
    }
}
