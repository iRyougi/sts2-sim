using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Hooks;

namespace Sts2Sim.Core.Models.Cards;

public sealed class RepeatShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 20m : 15m;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXEnergyCost => true;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        int x = Hook.ModifyXValue(CombatState!, this, cardPlay.Resources.EnergyXValue);
        if (x > 0)
        {
            AttackCommand cmd = DamageCmd.Attack(Damage).WithHitCount(x).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
            await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, cmd);
        }
    }
}
