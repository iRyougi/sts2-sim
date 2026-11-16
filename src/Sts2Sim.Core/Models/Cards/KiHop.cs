using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>攻击前目标已穿刺则抽牌（原版 <c>KiHop</c>）。</summary>
public sealed class KiHop : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Ki];
    private decimal Damage => IsUpgraded ? 8m : 7m;
    private int Cards => IsUpgraded ? 3 : 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Cards: Cards);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        bool wasImpaled = ArrowCmd.CreatureIsImpaledByArrow(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        if (wasImpaled)
        {
            await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
        }
    }
}
