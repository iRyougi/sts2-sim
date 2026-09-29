using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>SharedFate</c>：0 费、消耗，先让自己失去 2 力量，再让一名敌人失去 2（升级 3）力量。</summary>
public sealed class SharedFate : CardModel, ICardChoiceBaseValueProvider
{
    private const decimal PlayerStrengthLoss = 2m;

    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 0;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    private decimal EnemyStrengthLoss => IsUpgraded ? 3m : 2m;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<StrengthPower>(CombatState!, Owner.Creature, -PlayerStrengthLoss, Owner.Creature, this);
        await PowerCmd.Apply<StrengthPower>(CombatState!, cardPlay.Target, -EnemyStrengthLoss, Owner.Creature, this);
    }
}
