using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>造成伤害并下回合获得能量（原版 <c>KiWhip</c>）。</summary>
public sealed class KiWhip : CardModel, ICardChoiceBaseValueProvider, ICardDamageVariableProvider
{
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override IReadOnlyCollection<CardTag> CanonicalTags => [CardTag.Ki];
    private decimal Damage => IsUpgraded ? 6m : 5m;
    private int Energy => IsUpgraded ? 3 : 2;
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Damage: (double)Damage, Energy: Energy);
    public bool TryGetThrashDamageVariable(out decimal amount) { amount = Damage; return true; }

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        ArgumentNullException.ThrowIfNull(cardPlay.Target);
        await DamageCmd.Attack(Damage).FromCard(this, cardPlay).Targeting(cardPlay.Target).Execute();
        await PowerCmd.Apply<EnergyNextTurnPower>(CombatState!, Owner.Creature, Energy, Owner.Creature, this);
    }
}
