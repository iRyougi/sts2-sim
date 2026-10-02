using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

public sealed class PhylacteryUnbound : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override async Task BeforeCombatStart() => await OstyCmd.Summon(Owner, 5m, this);

    public override async Task AfterSideTurnStart(
        CombatSide side, IReadOnlyList<Creature> participants)
    {
        if (participants.Contains(Owner.Creature))
            await OstyCmd.Summon(Owner, 2m, this);
    }
}
