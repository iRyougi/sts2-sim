using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PaelsTears : RelicModel
{
    private bool _gainNextTurn;
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task BeforeSideTurnEnd(CombatSide side, IEnumerable<Creature> participants)
    {
        if (side == CombatSide.Player && participants.Contains(Owner.Creature))
            _gainNextTurn = Owner.PlayerCombatState!.Energy > 0;
        return Task.CompletedTask;
    }
    public override Task AfterEnergyReset(Player player)
    {
        if (player == Owner && _gainNextTurn) player.PlayerCombatState!.GainEnergy(2m);
        return Task.CompletedTask;
    }
    public override Task AfterCombatEnd() { _gainNextTurn = false; return Task.CompletedTask; }
    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) => builder.Append(_gainNextTurn);
}
