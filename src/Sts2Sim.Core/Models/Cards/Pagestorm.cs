using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>Pagestorm</c>：1（升级 0）费能力，获得 1 层 <see cref="PagestormPower"/>。</summary>
public sealed class Pagestorm : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Uncommon;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    private const int Cards = 1;

    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: Cards);

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<PagestormPower>(CombatState!, Owner.Creature, Cards, Owner.Creature, this);

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
