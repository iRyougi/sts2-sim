using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SneckoEye : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
    public override Task AfterObtained() =>
        Owner.Creature.CombatState is { } state && state.IsLiveCombat() ? ApplyPower() : Task.CompletedTask;
    public override Task BeforeCombatStart() => ApplyPower();
    public override decimal ModifyHandDraw(Player player, decimal originalCardCount) =>
        player == Owner ? originalCardCount + 2m : originalCardCount;

    // #328: the upstream TestMode energy override and animation are intentionally omitted.
    private Task ApplyPower() =>
        PowerCmd.Apply<ConfusedPower>(Owner.Creature.CombatState!, Owner.Creature, 1m, Owner.Creature, null);
}
