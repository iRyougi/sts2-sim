using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Models.Potions;

public sealed class OrobicAcid : PotionModel
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.AnyPlayer;

    protected override async Task OnUse(Creature? target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Player player = target.Player
            ?? throw new InvalidOperationException("Orobic Acid requires a player target.");
        ICombatState combatState = target.CombatState!;

        // 偏离 #68/#131 已销案（偏离 #319，2026-09-09）：候选池是接收者角色自己的卡池。
        // 原版对三种类型各调一次 GetDistinctForCombat(..., 1, rng)，即整池洗牌后取第一张，
        // 每次消耗"候选数 - 1"个随机数；三张全部抽完才一起加入手牌，所以抽取之间不会插入生成钩子。
        IEnumerable<CardModel> unlocked = player.Character.CardPool.GetUnlockedCards(
            player.UnlockState,
            isMultiplayer: player.RunState.Players.Count > 1);
        var generated = new List<CardModel>();
        foreach (CardType type in new[] { CardType.Attack, CardType.Skill, CardType.Power })
        {
            generated.AddRange(CardFactory.GetDistinctForCombat(
                player,
                unlocked.Where(card => card.Type == type),
                1,
                player.RunState.Rng.CombatCardGeneration));
        }

        foreach (CardModel card in generated)
        {
            card.SetToFreeThisTurn();
        }

        foreach (CardModel card in generated)
        {
            await CardPileCmd.Generate(combatState, card, PileType.Hand, Owner);
        }
    }
}
