using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>对目标施加 3（升级 4）层虚弱与易伤并消耗（原版 <c>TheEmpress</c>）。</summary>
public sealed class TheEmpress : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private decimal Amount => IsUpgraded ? 4m : 3m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<WeakPower>(CombatState!, cardPlay.Target, Amount, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(CombatState!, cardPlay.Target, Amount, Owner.Creature, this);
    }
}
