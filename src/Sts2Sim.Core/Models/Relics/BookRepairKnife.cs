using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>每有一个非自己、且所有能力都让死亡计为致命的生物死于 Doom，回复 3 点生命。</summary>
public sealed class BookRepairKnife : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterDiedToDoom(IReadOnlyList<Creature> creatures)
    {
        int count = creatures.Count(creature => creature != Owner.Creature &&
            creature.Powers.All(power => power.ShouldOwnerDeathTriggerFatal()));
        if (count > 0)
            await CreatureCmd.Heal(Owner.Creature, 3m * count);
    }
}
