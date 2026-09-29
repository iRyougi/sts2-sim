using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;

namespace Sts2Sim.Core.Commands;

public readonly record struct SummonResult(Creature? Osty, decimal Amount);

/// <summary>原版 <c>OstyCmd</c>。</summary>
public static class OstyCmd
{
    /// <summary>召唤：Osty 存活时增加最大生命；死去但留在战斗里时复活；从未出现过则新建并施加
    /// <see cref="DieForYouPower"/>。原版的召唤历史（<c>SummonedEntry</c>）没有玩法读取方，这里不记录。</summary>
    public static async Task<SummonResult> Summon(Player summoner, decimal amount, AbstractModel? source)
    {
        ArgumentNullException.ThrowIfNull(summoner);
        ICombatState combatState = summoner.Creature.CombatState
            ?? throw new InvalidOperationException("Summoner must be in combat.");
        amount = Hook.ModifySummonAmount(combatState, summoner, amount, source);
        if (amount == 0m)
            return new SummonResult(summoner.Osty, 0m);

        Creature? osty = combatState.Allies.FirstOrDefault(
            creature => creature.Monster is Osty && creature.PetOwner == summoner);
        if (summoner.IsOstyAlive)
        {
            await CreatureCmd.GainMaxHp(summoner.Osty!, amount);
        }
        else
        {
            bool isReviving = osty is not null;
            if (isReviving)
            {
                if (osty!.IsAlive)
                    throw new InvalidOperationException("Osty is alive but not registered as the summoner's pet.");
                summoner.PlayerCombatState!.AddPetInternal(osty);
            }
            else
            {
                osty = await PlayerCmd.AddPet<Osty>(summoner);
                await PowerCmd.Apply<DieForYouPower>(combatState, osty, 1m, null, null);
            }

            await CreatureCmd.SetMaxHp(osty!, amount);
            await CreatureCmd.Heal(osty!, amount);
            if (isReviving)
                await Hook.AfterOstyRevived(combatState, osty!);
        }

        await Hook.AfterSummon(combatState, summoner, amount);
        return new SummonResult(summoner.Osty, amount);
    }
}
