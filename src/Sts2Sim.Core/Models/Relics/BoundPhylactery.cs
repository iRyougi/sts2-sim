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
        // 原版判断 TurnNumber != 1；本引擎在 AfterEnergyReset 之后才加一回合数，0 是首回合的等价时刻。
        if (player == Owner && Owner.PlayerCombatState!.TurnNumber != 0)
            await SummonPet();
    }

    private Task SummonPet() => OstyCmd.Summon(Owner, 1m, this);
}
