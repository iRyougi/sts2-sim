using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Friendship</c>：失去 2（升级 1）力量，获得 1 层 <see cref="FriendshipPower"/>（最大能量 +1）。</summary>
public sealed class Friendship : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private decimal StrengthLoss => IsUpgraded ? 1m : 2m;

    private const int Energy = 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Energy: Energy);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<StrengthPower>(CombatState!, Owner.Creature, -StrengthLoss, Owner.Creature, this);
        await PowerCmd.Apply<FriendshipPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
    }
}
