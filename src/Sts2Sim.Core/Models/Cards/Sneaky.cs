using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Sneaky : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    public override bool IsMultiplayerOnly => true;
    protected override int CanonicalEnergyCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];
    protected override Task OnPlay(CardPlay cardPlay) =>
        PowerCmd.Apply<SneakyPower>(CombatState!, Owner.Creature, IsUpgraded ? 2m : 1m, Owner.Creature, this);
}
