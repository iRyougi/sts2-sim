using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>兼带 Wah/Cho/Ki 三标签，造成伤害、获得格挡并抽牌（原版 <c>WahChoKi</c>）。</summary>
public sealed class WahChoKi : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah, CardTag.Cho, CardTag.Ki];
    private decimal Damage => IsUpgraded ? 12m : 9m;
    private decimal Block => IsUpgraded ? 12m : 9m;
    private int Cards => IsUpgraded ? 2 : 1;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Block: (double)Block, Cards: Cards);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await CardPileCmd.Draw(CombatState!, Cards, Owner, fromHandDraw: false);
    }
}
