using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;

namespace Sts2Sim.Core.Models.Relics;

/// <summary>亡灵契约师起始遗物：开战时召唤 1，此后每个非首回合的能量重置后再召唤 1。</summary>
public sealed class BoundPhylactery : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;

    public override Task BeforeCombatStart() => SummonPet();

    public override async Task AfterEnergyResetLate(Player player)
    {
        if (player == Owner && Owner.PlayerCombatState!.TurnNumber != 1)
            await SummonPet();
    }

    private Task SummonPet() => OstyCmd.Summon(Owner, 1m, this);
}
