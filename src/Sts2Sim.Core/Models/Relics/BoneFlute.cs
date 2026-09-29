using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Commands.Builders;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>自己的 Osty 每次攻击后，获得 2 点格挡（Unpowered）。</summary>
public sealed class BoneFlute : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public override async Task AfterAttack(AttackCommand command)
    {
        if (command.Attacker?.Monster is not Osty || command.Attacker.PetOwner?.Creature != Owner.Creature)
            return;

        await CreatureCmd.GainBlock(Owner.Creature.CombatState!, Owner.Creature, 2m, ValueProp.Unpowered,
            cardSource: null, cardPlay: null);
    }
}
