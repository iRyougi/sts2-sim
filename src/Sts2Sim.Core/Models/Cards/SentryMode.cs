using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>SentryMode</c>：2 费（升级 1 费）能力，获得 1 层 <see cref="SentryModePower"/>。</summary>
public sealed class SentryMode : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 2;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<SentryModePower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);

    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
