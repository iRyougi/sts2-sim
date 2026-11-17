using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>失去 6（升级 3）点生命，获得 1 个额外回合并消耗（原版 <c>TheHangedMan</c>）。</summary>
public sealed class TheHangedMan : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private decimal HpLoss => IsUpgraded ? 3m : 6m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await CreatureCmd.Damage(
            CombatState!,
            [Owner.Creature],
            HpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
            Owner.Creature,
            this,
            cardPlay);
        await PowerCmd.Apply<ExtraTurnPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, null);
    }
}
