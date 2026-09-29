using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Rewards;

namespace Sts2Sim.Core.Commands;

/// <summary>Player-level resource commands (gold/stars)。偏离 #65：<c>GainGold</c>/<c>LoseGold</c>
/// 省略 SFX 音量分档与 <c>wasStolenBack</c>/history 记录参数（存档与多人历史记录均未移植）。</summary>
public static class PlayerCmd
{
    public static void CompleteQuest(Models.CardModel questCard)
    {
        if (questCard.Owner.RunState is Runs.RunState run) run.RecordQuestCompleted(questCard.Id);
    }

    public static async Task GainGold(decimal amount, Player player)
    {
        decimal modifiedAmount = Hook.ModifyGoldGained(player.RunState, player, amount);
        if (modifiedAmount > 0m)
        {
            player.Gold += (int)modifiedAmount;
            await Hook.AfterGoldGained(player.RunState, player);
        }
    }

    public static Task LoseGold(decimal amount, Player player, GoldLossType goldLossType = GoldLossType.Lost)
    {
        player.Gold = Math.Max(0, player.Gold - (int)amount);
        return Task.CompletedTask;
    }

    public static Task GainEnergy(decimal amount, Player player)
    {
        player.PlayerCombatState?.GainEnergy(amount);
        return Task.CompletedTask;
    }

    public static async Task GainStars(decimal amount, Player player)
    {
        if (player.PlayerCombatState is null)
        {
            return;
        }

        player.PlayerCombatState.GainStars(amount);
        ICombatState? combatState = player.Creature.CombatState;
        if (combatState is not null && amount > 0m)
        {
            await Hook.AfterStarsGained(combatState, (int)amount, player);
        }
    }

    public static Task LoseStars(decimal amount, Player player)
    {
        player.PlayerCombatState?.LoseStars(amount);
        return Task.CompletedTask;
    }

    public static async Task<Creature> AddPet<T>(Player owner) where T : MonsterModel
    {
        ArgumentNullException.ThrowIfNull(owner);
        ICombatState combatState = owner.Creature.CombatState
            ?? throw new InvalidOperationException("Pet owner must be in combat.");
        var monster = (T)ModelDb.Monster<T>().MutableClone();
        Creature pet = combatState.CreateCreature(monster, owner.Creature.Side, slotName: null);
        await AddPet(pet, owner);
        return pet;
    }

    public static async Task AddPet(Creature pet, Player owner)
    {
        ArgumentNullException.ThrowIfNull(pet);
        ArgumentNullException.ThrowIfNull(owner);
        if (pet.CombatState is null)
        {
            throw new InvalidOperationException("Pet must already be attached to a combat state.");
        }

        PlayerCombatState playerCombatState = owner.PlayerCombatState
            ?? throw new InvalidOperationException("Pet owner must have an active combat state.");
        playerCombatState.AddPetInternal(pet);
        await CreatureCmd.Add(pet);
    }

    /// <summary>Heals through the same 30%-of-max and rest-site modifier fold used by a normal rest.</summary>
    public static async Task MimicRestSiteHeal(Player player, bool playSfx = true)
    {
        ArgumentNullException.ThrowIfNull(player);
        _ = playSfx; // 偏离 #14：headless 模拟器统一剥离 Godot UI、视觉与音效表面。
        decimal healAmount = player.Creature.MaxHp * 0.3m;
        healAmount = Hook.ModifyRestSiteHealAmount(
            player.RunState,
            player.Creature,
            healAmount);
        await CreatureCmd.Heal(player.Creature, healAmount);
        await Hook.AfterRestSiteHeal(player.RunState, player, isMimicked: true);
        var rewards = new List<Reward>();
        Hook.ModifyRestSiteHealRewards(player.RunState, player, rewards, isMimicked: true);
        await RewardsCmd.OfferCustom(player, rewards);
    }
}
