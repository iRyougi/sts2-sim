using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Cards;

public sealed class Amnesia : CardModel, KnowledgeDemon.IChoosable, ICardChoiceBaseValueProvider
{
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => -1;
    // Native Amnesia.CanonicalVars: PowerVar<AmnesiaPower>(1), CardsVar(2), EnergyVar(1).
    public CardChoiceBaseValues? CardChoiceBaseValues => new(Cards: 2, Energy: 1);

    public Task OnChosen() => PowerCmd.Apply<AmnesiaPower>(CombatState!, Owner.Creature, 1m, Owner.Creature, this);
}
