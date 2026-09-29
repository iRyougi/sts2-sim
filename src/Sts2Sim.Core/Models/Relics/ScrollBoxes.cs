using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Relics;

public sealed class ScrollBoxes : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Ancient;

    /// <summary>偏离 #168 已销案（偏离 #304，2026-09-08）：权威 <c>CanGenerateBundles</c> 数的是
    /// 角色卡池中已解锁的 4 张普通牌与 2 张罕见牌，
    /// 不是扁平的全部非无色卡。角色卡池现已存在。</summary>
    public override bool IsAllowedAtNeow(IRunState runState)
    {
        // Neow 候选筛选走的是未 AssignOwner 的克隆（见 Neow.IsAllowedAtNeow），
        // 因此这里必须从 runState 取玩家，不能读 Owner。
        Player? player = runState.Players.FirstOrDefault();
        if (player is null)
        {
            return false;
        }

        IReadOnlyList<CardModel> pool = player.Character.CardPool
            .GetUnlockedCards(player.UnlockState, isMultiplayer: runState.Players.Count > 1)
            .ToArray();
        return pool.Count(card => card.Rarity == CardRarity.Common) >= 4 &&
            pool.Count(card => card.Rarity == CardRarity.Uncommon) >= 2;
    }

    public override async Task AfterObtained()
    {
        // Both visible bundles are instantiated before the player chooses, as in the native game.
        var bundles = GenerateRandomBundles(Owner)
            .Select(bundle => (IReadOnlyList<CardModel>)bundle.Select(canonical =>
            {
                var card = (CardModel)canonical.MutableClone();
                card.AssignOwner(Owner);
                return card;
            }).ToArray()).ToArray();
        foreach (CardModel card in await CardSelectCmd.FromChooseABundleScreen(Owner, bundles, this))
            await CardPileCmd.AddToDeck(card);
    }

    /// <summary>Generates two bundles. Defect has an independent 1% three-Claw branch per bundle.</summary>
    public static List<IReadOnlyList<CardModel>> GenerateRandomBundles(Player player)
    {
        var rewards = player.PlayerRng.Rewards;
        bool isDefect = player.Character is Defect;
        var pool = player.Character.CardPool;
        var commonOptions = CardCreationOptions.ForNonCombatWithUniformOdds(
                [pool], card => card.Rarity == CardRarity.Common)
            .WithFlags(CardCreationFlags.NoRarityModification);
        commonOptions = Hook.ModifyCardRewardCreationOptions(player.RunState, player, commonOptions);
        var uncommonOptions = CardCreationOptions.ForNonCombatWithUniformOdds(
                [pool], card => card.Rarity == CardRarity.Uncommon)
            .WithFlags(CardCreationFlags.NoRarityModification);
        uncommonOptions = Hook.ModifyCardRewardCreationOptions(player.RunState, player, uncommonOptions);

        List<CardModel> commons = commonOptions.GetPossibleCards(player).ToList();
        List<CardModel> uncommons = uncommonOptions.GetPossibleCards(player).ToList();
        var bundles = new List<IReadOnlyList<CardModel>>();
        var usedCardIds = new HashSet<ModelId>();
        for (int bundleIndex = 0; bundleIndex < 2; bundleIndex++)
        {
            if (isDefect && rewards.NextInt(100) < 1)
            {
                CardModel claw = ModelDb.Card<Claw>();
                bundles.Add([claw, claw, claw]);
                continue;
            }
            var bundle = new List<CardModel>();
            List<CardModel> remainingCommons = commons.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            for (int commonIndex = 0; commonIndex < 2; commonIndex++)
            {
                CardModel card = rewards.NextItem(remainingCommons)
                    ?? throw new InvalidOperationException("ScrollBoxes needs four distinct common cards.");
                bundle.Add(card);
                usedCardIds.Add(card.Id);
                remainingCommons.Remove(card);
            }

            List<CardModel> remainingUncommons = uncommons.Where(card => !usedCardIds.Contains(card.Id)).ToList();
            CardModel uncommon = rewards.NextItem(remainingUncommons)
                ?? throw new InvalidOperationException("ScrollBoxes needs two distinct uncommon cards.");
            bundle.Add(uncommon);
            usedCardIds.Add(uncommon.Id);
            bundles.Add(bundle);
        }

        return bundles;
    }
}
