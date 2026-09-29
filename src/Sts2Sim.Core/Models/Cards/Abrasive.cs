using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Abrasive : CardModel
{
    public override CardType Type => CardType.Power;
    public override CardRarity Rarity => CardRarity.Rare;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 3;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Sly];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        await PowerCmd.Apply<DexterityPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
        await PowerCmd.Apply<ThornsPower>(CombatState!, Owner.Creature, IsUpgraded ? 6m : 4m, Owner.Creature, this);
    }
}
