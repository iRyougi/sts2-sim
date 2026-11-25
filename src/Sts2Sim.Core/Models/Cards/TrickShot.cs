using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>射出箭矢造成 12（升级 14）点伤害；射出成功则抽 2（升级 3）张（v0.114.0 原版 <c>TrickShot</c>）。</summary>
public sealed class TrickShot : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private decimal Damage => IsUpgraded ? 14m : 12m;
    private int Cards => IsUpgraded ? 3 : 2;

    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Shoot];

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: Cards);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = Damage;
        return true;
    }

    protected override bool ShouldGlowRedInternal => !ArrowCmd.PlayerHasAnyArrows(Owner);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        AttackCommand cmd = DamageCmd.Attack(Damage).FromArrow().FromCard(this, cardPlay).Targeting(cardPlay.Target);
        if (await ArrowCmd.ShootArrowAtTarget(cardPlay.Target, Owner, cmd))
        {
            await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        }
    }
}
