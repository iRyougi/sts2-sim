using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>0 费兼带 Wah 与 Cho 标签，同时提供格挡与伤害（原版 <c>WahCho</c>）。</summary>
public sealed class WahCho : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override bool GainsBlock => true;
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Uncommon;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Wah, CardTag.Cho];
    private decimal Damage => IsUpgraded ? 5m : 3m;
    private decimal Block => IsUpgraded ? 5m : 3m;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Block: (double)Block);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await CreatureCmd.GainBlock(CombatState!, Owner.Creature, Block, ValueProp.Move, this, cardPlay);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
    }
}
