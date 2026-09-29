using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Commands;

namespace Sts2Sim.Core.Models.Relics;

public sealed class SturdyClamp : RelicModel
{
    private const decimal MaximumRetainedBlock = 10m;

    public override RelicRarity Rarity => RelicRarity.Rare;

    public override bool ShouldClearBlock(Creature creature) => creature != Owner.Creature;

    public override async Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature)
    {
        if (preventer != this || creature != Owner.Creature) return;
        if (creature.Block > MaximumRetainedBlock)
        {
            await CreatureCmd.LoseBlock(creature.CombatState!, creature,
                creature.Block - MaximumRetainedBlock, null);
        }
    }
}
