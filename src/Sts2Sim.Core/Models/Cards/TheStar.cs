using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>下回合额外获得 3（升级 4）点能量与抽 3（升级 4）张牌并消耗（原版 <c>TheStar</c>）。</summary>
public sealed class TheStar : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    private int Amount => IsUpgraded ? 4 : 3;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Amount, Energy: Amount);

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, Amount, Owner.Creature, this);
        await PowerCmd.Apply<DrawCardsNextTurnPower>(CombatState!, Owner.Creature, Amount, Owner.Creature, this);
    }
}
