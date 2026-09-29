namespace Sts2Sim.Core.Models.Cards;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;

public sealed class WasteAway : CardModel, KnowledgeDemon.IChoosable
{
    public override CardType Type => CardType.Status;
    public override CardRarity Rarity => CardRarity.Status;
    public override TargetType TargetType => TargetType.None;
    public override int MaxUpgradeLevel => 0;
    public override bool CanBeGeneratedInCombat => false;
    protected override int CanonicalEnergyCost => -1;
    public Task OnChosen() => PowerCmd.Apply<WasteAwayPower>(CombatState!, Owner.Creature, 1m, null, this);
}
