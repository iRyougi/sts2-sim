using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>RightHandHand</c>：0 费 Osty 攻击 4（升级 6）；Osty 不在时无伤害。
/// 自己打出一张结算能量值不少于 2 的牌后，若本牌在弃牌堆，则回到手牌（AfterCardPlayedLate）。</summary>
public sealed class RightHandHand : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    private const int EnergyThreshold = 2;

    public override CardType Type => CardType.Attack;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.OstyAttack];

    private decimal OstyDamage => IsUpgraded ? 6m : 4m;

    // OstyDamage 不是字面键；Energy 是原版 EnergyVar(2) 的字面值。
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: EnergyThreshold);

    public bool TryGetThrashDamageVariable(out decimal amount)
    {
        amount = OstyDamage;
        return true;
    }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (Owner.IsOstyMissing)
            return;

        await DamageCmd.Attack(OstyDamage).FromOsty(Owner.Osty!, this, cardPlay)
            .Targeting(cardPlay.Target).Execute();
    }

    public override Task AfterCardPlayedLate(CardPlay cardPlay)
    {
        if (cardPlay.Card.Owner == Owner &&
            cardPlay.Resources.EnergyValue >= EnergyThreshold &&
            Pile?.Type == PileType.Discard)
        {
            CardPileCmd.Add(this, PileType.Hand);
        }

        return Task.CompletedTask;
    }
}
