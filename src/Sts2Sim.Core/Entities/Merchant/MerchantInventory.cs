using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Entities.Merchant;

/// <summary>
/// Generates normal merchant inventory with five character-card slots and two colorless-card slots.
/// </summary>
public sealed class MerchantInventory
{
    private static readonly CardType[] CardSlotTypes =
    {
        CardType.Attack, CardType.Attack, CardType.Skill, CardType.Skill, CardType.Power,
    };

    private readonly Player _player;
    private readonly List<MerchantCardEntry> _cards;
    private readonly List<MerchantRelicEntry> _relics;
    private readonly List<MerchantPotionEntry> _potions;

    /// <summary>The player for whom this stock and its prices were generated.</summary>
    public Player Player => _player;

    public IReadOnlyList<MerchantCardEntry> Cards => _cards;
    public IReadOnlyList<MerchantRelicEntry> Relics => _relics;
    public IReadOnlyList<MerchantPotionEntry> Potions => _potions;

    public MerchantCardRemovalEntry CardRemoval { get; }

    private MerchantInventory(
        Player player,
        List<MerchantCardEntry> cards,
        List<MerchantRelicEntry> relics,
        List<MerchantPotionEntry> potions,
        MerchantCardRemovalEntry cardRemoval)
    {
        _player = player;
        _cards = cards;
        _relics = relics;
        _potions = potions;
        CardRemoval = cardRemoval;
    }

    public static MerchantInventory Generate(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        using IDisposable rngScope = player.PlayerRng.BeginSemanticScope(
            $"{player.CurrentSemanticLocationKey}/merchant/phase=initial_inventory");
        Rng shopRng = player.PlayerRng.Shops;
        List<MerchantCardEntry> cards = GenerateCardEntries(player, shopRng);
        List<MerchantRelicEntry> relics = GenerateRelicEntries(player, shopRng);
        List<MerchantPotionEntry> potions = GeneratePotionEntries(player, shopRng);
        var cardRemoval = new MerchantCardRemovalEntry(
            MerchantCardRemovalEntry.PriceFor(player.RunState.Ascension, player.CardRemovalsUsed),
            player);
        return new MerchantInventory(player, cards, relics, potions, cardRemoval);
    }

    public static MerchantInventory CreateForEvent(Player player, IEnumerable<MerchantRelicEntry> relics) =>
        new(player, [], relics.ToList(), [], new MerchantCardRemovalEntry(int.MaxValue, player));

    /// <summary>Plan06a 偏离 #80（无色卡槽位缺失）/#81（角色卡槽位候选通常为0）已在 Plan06b 铺开
    /// 91 张角色卡 + 65 张无色卡后解决——本类头部注释里的"five character-card slots and two
    /// colorless-card slots"就是解决后的结果，见 Plan06b 文档"完成情况"小节。这条注释说明的是解决后仍保留的
    /// 一个独立 RNG 消耗顺序细节：折扣槽位索引在生成任何卡牌之前抽取（逐字复刻真实游戏
    /// <c>MerchantInventory.PopulateCharacterCardEntries</c> 的消耗顺序：先 <c>NextInt</c> 选槽位，
    /// 再逐槽生成，命中槽位额外多消耗一次 <c>NextFloat</c> 重算价格），而不是先生成完全部价格再选槽位打折。</summary>
    private static List<MerchantCardEntry> GenerateCardEntries(Player player, Rng shopRng)
    {
        int discountIndex = shopRng.NextInt(CardSlotTypes.Length);
        var used = new HashSet<ModelId>();
        var entries = new List<MerchantCardEntry>();
        for (int i = 0; i < CardSlotTypes.Length; i++)
        {
            CardType type = CardSlotTypes[i];
            CardRarity rolledRarity = player.Odds.CardRarity.RollWithoutChangingFutureOdds(
                CardRarityOddsType.Shop,
                player.PlayerRng.Rewards);
            List<CardModel> candidates = CharacterMerchantCandidates(player, type, rolledRarity, used);
            CardModel? picked = shopRng.NextItem(candidates);
            if (picked is null)
            {
                continue;
            }

            used.Add(picked.Id);
            CardModel card = CreateMerchantCard(picked, player);
            int basePrice = CardBasePrice(card);
            int price = (int)Math.Round(basePrice * shopRng.NextFloat(0.95f, 1.05f));
            if (i == discountIndex)
            {
                price = MerchantCardEntry.CalculateDiscountedPrice(basePrice, shopRng);
            }

            entries.Add(new MerchantCardEntry(card, price, player));
        }

        foreach (CardRarity rarity in new[] { CardRarity.Uncommon, CardRarity.Rare })
        {
            List<CardModel> candidates = ColorlessCardPool.Instance.GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1)
                .Where(card =>
                    card.IsColorless &&
                    card.Rarity == rarity &&
                    !used.Contains(card.Id))
                .ToList();
            CardModel? picked = shopRng.NextItem(candidates);
            if (picked is null)
            {
                continue;
            }

            used.Add(picked.Id);
            CardModel card = CreateMerchantCard(picked, player);
            int price = (int)Math.Round(CardBasePrice(card) * shopRng.NextFloat(0.95f, 1.05f));
            entries.Add(new MerchantCardEntry(card, price, player));
        }

        return entries;
    }

    private static List<CardModel> CharacterMerchantCandidates(
        Player player,
        CardType type,
        CardRarity rolledRarity,
        IReadOnlySet<ModelId> used)
    {
        CardRarity rarity = rolledRarity;
        for (int i = 0; i < 3; i++)
        {
            List<CardModel> candidates = player.Character.CardPool.GetUnlockedCards(player.UnlockState, player.RunState.Players.Count > 1)
                .Where(card =>
                    !card.IsColorless &&
                    card.Type == type &&
                    card.Rarity == rarity &&
                    !used.Contains(card.Id))
                .ToList();
            if (candidates.Count > 0)
            {
                return candidates;
            }

            rarity = NextMerchantRarity(rarity);
        }

        return new List<CardModel>();
    }

    private static CardRarity NextMerchantRarity(CardRarity rarity) => rarity switch
    {
        CardRarity.Common => CardRarity.Uncommon,
        CardRarity.Uncommon => CardRarity.Rare,
        CardRarity.Rare => CardRarity.Common,
        _ => throw new ArgumentOutOfRangeException(nameof(rarity)),
    };

    private static List<MerchantRelicEntry> GenerateRelicEntries(Player player, Rng shopRng)
    {
        var entries = new List<MerchantRelicEntry>();
        foreach (RelicRarity rarity in new[]
        {
            RelicFactory.RollRarity(player.PlayerRng.Rewards),
            RelicFactory.RollRarity(player.PlayerRng.Rewards),
        })
        {
            RelicModel relic = RelicFactory.PullNextRelicFromBack(player, rarity);
            entries.Add(new MerchantRelicEntry(relic, RelicPrice(relic, shopRng), player));
        }

        RelicModel shopRelic = RelicFactory.PullNextRelicFromBack(player, RelicRarity.Shop);
        entries.Add(new MerchantRelicEntry(shopRelic, RelicPrice(shopRelic, shopRng), player));
        return entries;
    }

    private static List<MerchantPotionEntry> GeneratePotionEntries(Player player, Rng shopRng)
    {
        // Upstream generates a distinct batch before MerchantPotionEntry calculates any prices.
        var potions = new List<PotionModel>();
        var used = new HashSet<ModelId>();
        for (int i = 0; i < 3; i++)
        {
            PotionModel? potion = PotionFactory.CreateRandomOutOfCombat(player, shopRng,
                candidate => !used.Contains(candidate.Id));
            if (potion is null) continue;
            used.Add(potion.Id);
            potions.Add(potion);
        }
        return potions.Select(potion => new MerchantPotionEntry(potion,
            (int)Math.Round(PotionBasePrice(potion.Rarity) * shopRng.NextFloat(0.95f, 1.05f)), player)).ToList();
    }
    /// <summary>Equivalent to the card portion of PurchaseCompleted -> UpdateEntries -> UpdateEntry.</summary>
    internal void RefreshCardCreationResults()
    {
        foreach (MerchantCardEntry entry in _cards.Where(entry => !entry.Purchased))
        {
            entry.RefreshCardCreationResult(_player);
        }
    }

    /// <summary>Replaces a purchased stocked-item entry with a fresh entry in the same shelf slot.</summary>
    internal void Restock(MerchantEntry purchased)
    {
        switch (purchased)
        {
            case MerchantCardEntry card:
                RestockCard(card);
                break;
            case MerchantRelicEntry relic:
                RestockRelic(relic);
                break;
            case MerchantPotionEntry potion:
                RestockPotion(potion);
                break;
            default:
                throw new InvalidOperationException("Only stocked merchant items can be restocked.");
        }
    }

    private void RestockCard(MerchantCardEntry purchased)
    {
        int index = _cards.IndexOf(purchased);
        if (index < 0)
        {
            throw new InvalidOperationException("Purchased card entry is not in this inventory.");
        }

        using IDisposable rngScope = _player.PlayerRng.BeginSemanticScope(
            $"{_player.CurrentSemanticLocationKey}/merchant/restock=card/slot={index}");
        Rng shopRng = _player.PlayerRng.Shops;
        var used = _cards.Select(entry => entry.Card.Id).ToHashSet();
        CardModel? picked;
        if (!purchased.Card.IsColorless)
        {
            CardRarity rarity = _player.Odds.CardRarity.RollWithoutChangingFutureOdds(
                CardRarityOddsType.Shop,
                _player.PlayerRng.Rewards);
            List<CardModel> candidates = CharacterMerchantCandidates(_player, purchased.Card.Type, rarity, used);
            if (candidates.Count == 0)
            {
                candidates = CharacterMerchantCandidates(_player, purchased.Card.Type, rarity, new HashSet<ModelId>());
            }
            picked = shopRng.NextItem(candidates);
        }
        else
        {
            CardRarity rarity = purchased.Card.Rarity;
            List<CardModel> candidates = ColorlessCardPool.Instance.GetUnlockedCards(_player.UnlockState, _player.RunState.Players.Count > 1)
                .Where(card => card.IsColorless && card.Rarity == rarity && !used.Contains(card.Id))
                .ToList();
            if (candidates.Count == 0)
            {
                candidates = ColorlessCardPool.Instance.GetUnlockedCards(_player.UnlockState, _player.RunState.Players.Count > 1)
                    .Where(card => card.IsColorless && card.Rarity == rarity)
                    .ToList();
            }
            picked = shopRng.NextItem(candidates);
        }

        picked ??= ModelDb.GetById<CardModel>(purchased.Card.Id);
        CardModel card = CreateMerchantCard(picked, _player);
        int price = (int)Math.Round(CardBasePrice(card) * shopRng.NextFloat(0.95f, 1.05f));
        _cards[index] = new MerchantCardEntry(card, price, _player);
    }

    private void RestockRelic(MerchantRelicEntry purchased)
    {
        int index = _relics.IndexOf(purchased);
        if (index < 0)
        {
            throw new InvalidOperationException("Purchased relic entry is not in this inventory.");
        }

        using IDisposable rngScope = _player.PlayerRng.BeginSemanticScope(
            $"{_player.CurrentSemanticLocationKey}/merchant/restock=relic/slot={index}");
        Rng shopRng = _player.PlayerRng.Shops;
        RelicRarity rarity = RelicFactory.RollRarity(_player.PlayerRng.Rewards);
        RelicModel relic = RelicFactory.PullNextRelicFromBack(_player, rarity);
        _relics[index] = new MerchantRelicEntry(relic, RelicPrice(relic, shopRng), _player);
    }

    private void RestockPotion(MerchantPotionEntry purchased)
    {
        int index = _potions.IndexOf(purchased);
        if (index < 0)
        {
            throw new InvalidOperationException("Purchased potion entry is not in this inventory.");
        }

        using IDisposable rngScope = _player.PlayerRng.BeginSemanticScope(
            $"{_player.CurrentSemanticLocationKey}/merchant/restock=potion/slot={index}");
        Rng shopRng = _player.PlayerRng.Shops;
        PotionModel potion = PotionFactory.CreateRandomOutOfCombat(_player, shopRng) ??
            (PotionModel)ModelDb.GetById<PotionModel>(purchased.Potion.Id).MutableClone();
        int price = (int)Math.Round(PotionBasePrice(potion.Rarity) * shopRng.NextFloat(0.95f, 1.05f));
        _potions[index] = new MerchantPotionEntry(potion, price, _player);
    }

    private static CardModel CreateMerchantCard(CardModel canonical, Player player)
    {
        var card = (CardModel)canonical.MutableClone();
        card.AssignOwner(player);
        CardFactory.RollForMerchantUpgrade(player, card);
        return MerchantCardEntry.ModifyCreatedCard(card, player);
    }

    private static int CardBasePrice(CardModel card)
    {
        int price = card.Rarity switch
        {
            CardRarity.Rare => 150,
            CardRarity.Uncommon => 75,
            _ => 50,
        };
        return card.IsColorless ? (int)Math.Round(price * 1.15f) : price;
    }

    private static int PotionBasePrice(PotionRarity rarity) => rarity switch
    {
        PotionRarity.Rare => 100,
        PotionRarity.Uncommon => 75,
        _ => 50,
    };

    private static int RelicPrice(RelicModel relic, Rng shopRng) =>
        (int)Math.Round(relic.MerchantCost * shopRng.NextFloat(0.85f, 1.15f));
}
