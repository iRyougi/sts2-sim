using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Putrefy</c>：消耗，给目标施加 2（升级 3）虚弱，再施加同样层数的易伤。</summary>
public sealed class Putrefy : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private int PowerAmount => IsUpgraded ? 3 : 2;

    // "Power" 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, PowerAmount, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, PowerAmount, Owner.Creature, this);
    }
}
