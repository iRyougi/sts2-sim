using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Orbs;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Zap : CardModel, ICardChoiceBaseValueProvider
{
    public CardChoiceBaseValues? CardChoiceBaseValues => new();
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Basic;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 1;
    protected override Task OnPlay(CardPlay cardPlay) => OrbCmd.Channel<LightningOrb>(CombatState!, Owner);
    protected override void OnUpgrade() => ReduceEnergyCost(1);
}
