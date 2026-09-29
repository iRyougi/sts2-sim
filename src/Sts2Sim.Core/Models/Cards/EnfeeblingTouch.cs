using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>EnfeeblingTouch</c>：1 费虚无技能，本回合目标失去 8（升级 11）力量（<see cref="EnfeeblingTouchPower"/>）。</summary>
public sealed class EnfeeblingTouch : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.AnyEnemy;

    protected override int CanonicalEnergyCost => 1;

    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    private decimal StrengthLoss => IsUpgraded ? 11m : 8m;

    // StrengthLoss 不是 CombatSolver 的字面键。
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await PowerCmd.Apply<EnfeeblingTouchPower>(CombatState!, cardPlay.Target, StrengthLoss, Owner.Creature, this);
    }
}
