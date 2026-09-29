using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

public sealed class FakeSneckoEye : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Event;

    public override int MerchantCost => 50;

    public override Task AfterObtained() =>
        Owner.Creature.CombatState is { } combatState
            ? ApplyPower(combatState)
            : Task.CompletedTask;

    public override Task BeforeCombatStart() =>
        ApplyPower(Owner.Creature.CombatState!);

    private Task ApplyPower(Combat.ICombatState combatState) =>
        PowerCmd.Apply<ConfusedPower>(
            combatState,
            Owner.Creature,
            1m,
            Owner.Creature,
            null);
}
