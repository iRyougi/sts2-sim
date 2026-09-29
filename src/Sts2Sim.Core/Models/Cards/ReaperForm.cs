using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

/// <summary>原版 <c>ReaperForm</c>：3 费能力，获得 1 层 <see cref="ReaperFormPower"/>；升级获得保留。</summary>
public sealed class ReaperForm : CardModel, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Power;

    public override CardRarity Rarity => CardRarity.Rare;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 3;

    public CardChoiceBaseValues? CardChoiceBaseValues => new();

    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<ReaperFormPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}
