using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>每次洗牌后获得 5 层 AimPower（原版 <c>WhittlingKnife</c>）。</summary>
public sealed class WhittlingKnife : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;

    public override async Task AfterShuffle(Player shuffler)
    {
        if (shuffler == Owner && Owner.Creature.CombatState is { } combatState)
        {
            Flash();
            await PowerCmd.Apply<AimPower>(
                combatState, Owner.Creature, 5m, Owner.Creature, cardSource: null);
        }
    }
}
