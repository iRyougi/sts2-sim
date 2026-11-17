using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>若目标敌人当前 HP 不超过阈值则立即将其 HP 设为 0（原版 <c>Judgement</c>）。</summary>
public sealed class Judgement : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Threshold => IsUpgraded ? 40 : 30;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        if (cardPlay.Target.IsAlive && cardPlay.Target.CurrentHp <= Threshold)
        {
            await CreatureCmd.SetCurrentHp(cardPlay.Target, 0m);
        }
    }
}
