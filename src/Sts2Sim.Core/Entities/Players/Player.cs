using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.RelicPools;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Runs.Transplant;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Entities.Players;

/// <summary>
/// 玩家。偏离 #38：CreateForNewRun 签名精简为 (CharacterModel, IRunState)（游戏原版还有
/// NetId/UnlockState/共享 RelicGrabBag）；Relics/PotionSlots/PlayerRng/PlayerOdds/RelicGrabBag
/// 曾经留空，Plan06a 已全部接线填充，本条不再成立。
/// </summary>
public sealed class Player
{
    public const int InitialMaxPotionSlotCount = 3;

    public CharacterModel Character { get; }

    public PlayerUnlockState UnlockState { get; }

    public Creature Creature { get; }

    public IRunState RunState { get; private set; } = null!;

    public bool IsActiveForHooks { get; private set; }

    public PlayerCombatState? PlayerCombatState { get; private set; }

    /// <summary>本场战斗里这名玩家的 Osty；不在战斗中为 null，死去但留在战斗里时返回那只死去的 Osty。</summary>
    public Creature? Osty => PlayerCombatState?.GetPet<Models.Monsters.Osty>();

    public bool IsOstyAlive => Osty?.IsAlive ?? false;

    public bool IsOstyMissing => !IsOstyAlive;

    public int Gold { get; set; }

    public bool CanUseOrRemovePotions { get; set; } = true;

    public int CardRemovalsUsed { get; private set; }

    public CardPile Deck { get; } = new(PileType.Deck);

    public int MaxEnergy { get; private set; }

    public int BaseOrbSlotCount { get; set; }

    private readonly List<RelicModel> _relics = new();

    public IReadOnlyList<RelicModel> Relics => _relics;

    public bool HasEventPet() =>
        Relics.Any(relic => relic.AddsPet) ||
        Deck.Cards.Any(card => card is Models.Cards.ByrdonisEgg);

    private readonly List<PotionModel?> _potionSlots = new();

    public IReadOnlyList<PotionModel?> PotionSlots => _potionSlots;

    public int MaxPotionCount => _potionSlots.Count;

    public void GrowPotionSlots(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        for (int i = 0; i < count; i++)
        {
            _potionSlots.Add(null);
        }
    }

    public void SubtractFromMaxPotionCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (_potionSlots.Count(slot => slot is null) < count)
        {
            throw new InvalidOperationException("Cannot remove occupied potion slots.");
        }

        for (int i = 0; i < count; i++)
        {
            int emptySlotIndex = _potionSlots.LastIndexOf(null);
            _potionSlots.RemoveAt(emptySlotIndex);
        }
    }

    public RelicGrabBag RelicGrabBag { get; private set; } = null!;

    public PlayerRngSet PlayerRng { get; private set; } = null!;

    public PlayerOddsSet Odds { get; private set; } = null!;

    /// <summary>Derives a player RNG for a purpose owned by the currently visible room.</summary>
    public Rng RngForCurrentRoom(PlayerRngType stream, string purpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        AbstractRoom? room = RunState.CurrentRoom;
        string location = RunState is Runs.RunState concreteRun
            ? concreteRun.SemanticLocationKey
            : $"floor={RunState.TotalFloor}/room_kind={room?.RoomType.ToString() ?? "none"}/room_id={room?.Id ?? 0}";
        return PlayerRng.ForSemanticKey(
            stream,
            $"{location}/{purpose}");
    }

    public string CurrentSemanticLocationKey => RunState is Runs.RunState concreteRun
        ? concreteRun.SemanticLocationKey
        : $"floor={RunState.TotalFloor}/room_kind={RunState.CurrentRoom?.RoomType.ToString() ?? "none"}" +
          $"/room_id={RunState.CurrentRoom?.Id ?? 0}";

    public IEnumerable<CardPile> Piles => PlayerCombatState == null
        ? new[] { Deck }
        : PlayerCombatState.AllPiles.Append(Deck);

    private Player(
        CharacterModel character,
        int currentHp,
        int maxHp,
        int maxEnergy,
        int gold,
        PlayerUnlockState unlockState)
    {
        Character = character;
        UnlockState = unlockState;
        Creature = new Creature(this, currentHp, maxHp);
        MaxEnergy = maxEnergy;
        BaseOrbSlotCount = character.BaseOrbSlotCount;
        Gold = gold;
        IsActiveForHooks = Creature.IsAlive;
        for (int i = 0; i < InitialMaxPotionSlotCount; i++)
        {
            _potionSlots.Add(null);
        }
    }

    public static Player CreateForNewRun(
        CharacterModel character,
        IRunState runState,
        PlayerUnlockState? unlockState = null)
    {
        var player = new Player(
            character,
            character.StartingHp,
            character.StartingHp,
            character.MaxEnergy,
            character.StartingGold,
            unlockState ?? PlayerUnlockState.AllUnlocked())
        {
            RunState = runState,
        };
        player.PlayerRng = runState.Rng.UsesSemanticKeys
            ? PlayerRngSet.CreateKeyed(runState.Rng.Seed)
            : new PlayerRngSet(runState.Rng.Seed);
        player.Odds = new PlayerOddsSet(player.PlayerRng, runState.Ascension, new HookOddsAdapter(runState));
        player.PopulateStartingDeck();
        foreach (Type relicType in character.StartingRelics)
        {
            var canonical = ModelDb.GetById<RelicModel>(ModelDb.GetId(relicType));
            var copy = (RelicModel)canonical.MutableClone();
            copy.AssignOwner(player);
            player.AddRelicInternal(copy);
        }
        player.RelicGrabBag = runState is RunState concreteRun
            ? concreteRun.CreatePlayerRelicGrabBag(player)
            : new RelicGrabBag(
                runState.Rng.ForSemanticKey(
                    RunRngType.TreasureRoomRelics,
                    $"run_setup/relic_bag/character={character.Id.Entry}"),
                character.RelicPool.AllRelics.Concat(SharedRelicPool.Instance.AllRelics));
        runState.Ascension.ApplyEffectsTo(player);
        return player;
    }

    private void PopulateStartingDeck()
    {
        foreach (Type cardType in Character.StartingDeck)
        {
            var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(cardType));
            var copy = (CardModel)canonical.MutableClone();
            copy.FloorAddedToDeck = 1;
            copy.AssignOwner(this);
            Deck.AddInternal(copy);
        }
    }

    public void AddRelicInternal(RelicModel relic) => _relics.Add(relic);

    internal int RemoveRelicInternal(RelicModel relic)
    {
        int index = _relics.IndexOf(relic);
        if (index < 0)
        {
            throw new InvalidOperationException("Relic is not in the player inventory.");
        }

        _relics.RemoveAt(index);
        return index;
    }

    internal void InsertRelicInternal(int index, RelicModel relic) => _relics.Insert(index, relic);

    public void IncrementCardRemovalsUsed() => CardRemovalsUsed++;

    public PotionModel AddPotionInternal(PotionModel potion)
    {
        if (_potionSlots.Contains(potion))
        {
            throw new InvalidOperationException("Potion is already in a slot.");
        }

        int emptyIndex = _potionSlots.IndexOf(null);
        if (emptyIndex < 0)
        {
            throw new InvalidOperationException("No empty potion slot available.");
        }

        PotionModel ownedPotion = potion.IsCanonical
            ? (PotionModel)potion.MutableClone()
            : potion;
        if (ownedPotion.Owner is not null && !ReferenceEquals(ownedPotion.Owner, this))
        {
            throw new InvalidOperationException("Potion is owned by another player.");
        }

        ownedPotion.AssignOwner(this);
        _potionSlots[emptyIndex] = ownedPotion;
        return ownedPotion;
    }

    public void RemovePotionInternal(PotionModel potion)
    {
        int index = _potionSlots.IndexOf(potion);
        if (index >= 0)
        {
            _potionSlots[index] = null;
        }
    }
    public void ResetCombatState()
    {
        Creature.ResetCumulativeHpLost();
        PlayerCombatState = new PlayerCombatState(this);
    }

    /// <summary>把牌库克隆进抽牌堆并洗牌。偏离：省略游戏签名里未被使用的 CombatState 参数
    /// （克隆体的 CombatState 关联由 <c>CombatState.AddPlayerCreature</c> 统一设置,见 Task 13）。</summary>
    public void PopulateCombatState(Rng shuffleRng)
    {
        foreach (CardModel deckCard in Deck.Cards)
        {
            var combatCopy = (CardModel)deckCard.MutableClone();
            combatCopy.AssignOwner(this);
            combatCopy.AssignDeckVersionInternal(deckCard);
            PlayerCombatState!.DrawPile.AddInternal(combatCopy);
        }
        PlayerCombatState!.DrawPile.RandomizeOrderInternal(shuffleRng);
        List<CardModel> initialOrder = PlayerCombatState.DrawPile.Cards.ToList();
        var bottomDesignatedCards = new HashSet<CardModel>(
            initialOrder.Where(card => card.Enchantments.Any(
                enchantment => enchantment.ShouldStartAtBottomOfDrawPile)),
            ReferenceEqualityComparer.Instance);
        foreach (EnchantmentModel enchantment in initialOrder
                     .SelectMany(card => card.Enchantments)
                     .ToList())
        {
            enchantment.ModifyShuffleOrder(this, initialOrder, isInitialShuffle: true);
        }
        foreach (CardModel card in initialOrder)
        {
            PlayerCombatState.DrawPile.MoveToBottomInternal(card);
        }

        // 偏离 #87：多张 Innate 卡移到牌堆顶时，未核实真实游戏中的相对顺序。
        foreach (CardModel innateCard in PlayerCombatState.DrawPile.Cards
                     .Where(card => card.HasKeyword(CardKeyword.Innate) && !bottomDesignatedCards.Contains(card))
                     .ToList())
        {
            PlayerCombatState.DrawPile.MoveToTopInternal(innateCard);
        }
    }

    public Task AfterCombatEnd()
    {
        ClearCombatForVictory();
        DetachCombatState();
        return Task.CompletedTask;
    }

    // Source combat teardown clears transient state before victory listeners, without
    // ordinary power-removal effects, while preserving the completed combat context.
    internal void ClearCombatForVictory()
    {
        foreach (PowerModel power in Creature.Powers.ToArray())
            Creature.RemovePowerInternal(power);
        if (PlayerCombatState is { } state)
        {
            foreach (var pile in state.AllPiles)
                foreach (CardModel card in pile.Cards.ToArray())
                    pile.RemoveInternal(card);
        }
        Creature.LoseBlockInternal(Creature.Block);
    }

    internal void DetachCombatState()
    {
        Creature.CombatState = null;
        PlayerCombatState = null;
    }
    internal void RestoreForTransplant(PlayerSnapshot snapshot)
    {
        if (snapshot.CurrentHp < 1 || snapshot.MaxHp < snapshot.CurrentHp || snapshot.Gold < 0 ||
            snapshot.CardRemovalsUsed < 0)
            throw new TransplantMappingException("Player", "Invalid player scalar value.");
        if (snapshot.Deck is null || snapshot.Relics is null || snapshot.PotionSlots is null)
            throw new TransplantMappingException("Player", "Missing inventory collection.");

        if (snapshot.MaxEnergy is null or < 0)
            throw new TransplantMappingException("Player.MaxEnergy", "Missing or invalid max energy.");
        if (snapshot.BaseOrbSlotCount is null or < 0 ||
            snapshot.BaseOrbSlotCount != Character.BaseOrbSlotCount)
            throw new TransplantMappingException("Player.BaseOrbSlotCount",
                "Orb slot state cannot be represented by this simulator character.");
        MaxEnergy = snapshot.MaxEnergy.Value;
        Creature.SetMaxHpInternal(snapshot.MaxHp);
        Creature.SetCurrentHpInternal(snapshot.CurrentHp);
        Gold = snapshot.Gold;
        CardRemovalsUsed = snapshot.CardRemovalsUsed;
        foreach (CardModel card in Deck.Cards.ToArray()) Deck.RemoveInternal(card);
        _relics.Clear();
        _potionSlots.Clear();

        for (int index = 0; index < snapshot.Deck.Count; index++)
        {
            CardSnapshot item = snapshot.Deck[index];
            string path = $"Player.Deck[{index}]";
            if (item is null || string.IsNullOrWhiteSpace(item.Id))
                throw new TransplantMappingException($"{path}.Id", "Missing card ID.");
            Deck.AddInternal(CreateCardForTransplant(item, path));
        }

        for (int index = 0; index < snapshot.Relics.Count; index++)
        {
            RelicSnapshot item = snapshot.Relics[index];
            string path = $"Player.Relics[{index}]";
            if (item is null || string.IsNullOrWhiteSpace(item.Id))
                throw new TransplantMappingException($"{path}.Id", "Missing relic ID.");
            RelicModel canonical;
            try { canonical = ModelDb.GetById<RelicModel>(ModelId.Deserialize(item.Id)); }
            catch (Exception error) when (error is System.Text.Json.JsonException or
                Sts2Sim.Core.Models.Exceptions.ModelNotFoundException or InvalidCastException)
            { throw new TransplantMappingException($"{path}.Id", error.Message); }
            TransplantFieldRegistry.AssertImportableModel(canonical, path);
            if (item.Props is null)
                throw new TransplantMappingException($"{path}.Props", "Missing properties.");
            RelicModel relic = (RelicModel)canonical.MutableClone();
            relic.AssignOwner(this);
            TransplantSavedProperties.Apply(relic, item.Props, path, relic: true);
            _relics.Add(relic);
        }

        for (int index = 0; index < snapshot.PotionSlots.Count; index++)
        {
            string? id = snapshot.PotionSlots[index];
            if (id is null) { _potionSlots.Add(null); continue; }
            if (string.IsNullOrWhiteSpace(id))
                throw new TransplantMappingException($"Player.PotionSlots[{index}]", "Empty potion ID.");
            PotionModel canonical;
            try { canonical = ModelDb.GetById<PotionModel>(ModelId.Deserialize(id)); }
            catch (Exception error) when (error is System.Text.Json.JsonException or
                Sts2Sim.Core.Models.Exceptions.ModelNotFoundException or InvalidCastException)
            { throw new TransplantMappingException($"Player.PotionSlots[{index}]", error.Message); }
            PotionModel potion = (PotionModel)canonical.MutableClone();
            potion.AssignOwner(this);
            _potionSlots.Add(potion);
        }

        PlayerRng.LoadFromSerializable(snapshot.PlayerRng);
        Odds.LoadFromSerializable(snapshot.PlayerOdds);
        RelicGrabBag = RelicGrabBag.ImportForTransplant(snapshot.RelicBag, "Player.RelicBag");
    }
    internal CardModel CreateCardForTransplant(CardSnapshot item, string path)
    {
        CardModel canonical;
        try { canonical = ModelDb.GetById<CardModel>(ModelId.Deserialize(item.Id)); }
        catch (Exception error) when (error is System.Text.Json.JsonException or
            Sts2Sim.Core.Models.Exceptions.ModelNotFoundException or InvalidCastException)
        { throw new TransplantMappingException($"{path}.Id", error.Message); }
        TransplantFieldRegistry.AssertImportableModel(canonical, path);
        if (item.UpgradeLevel < 0 || item.UpgradeLevel > canonical.MaxUpgradeLevel)
            throw new TransplantMappingException($"{path}.UpgradeLevel", "Invalid upgrade level.");
        if ((item.EnchantmentId is null) != (item.EnchantmentAmount is null))
            throw new TransplantMappingException($"{path}.EnchantmentId", "Incomplete enchantment.");
        if (item.Props is null)
            throw new TransplantMappingException($"{path}.Props", "Missing properties.");
        CardModel card = (CardModel)canonical.MutableClone();
        card.AssignOwner(this);

        TransplantSavedProperties.Apply(card, item.Props, path, relic: false);
        card.FloorAddedToDeck = item.FloorAddedToDeck;
        if (item.EnchantmentId is not null)
        {
            EnchantmentModel canonicalEnchantment;
            try { canonicalEnchantment = ModelDb.GetById<EnchantmentModel>(
                ModelId.Deserialize(item.EnchantmentId)); }
            catch (Exception error) when (error is System.Text.Json.JsonException or
                Sts2Sim.Core.Models.Exceptions.ModelNotFoundException or InvalidCastException)
            { throw new TransplantMappingException($"{path}.EnchantmentId", error.Message); }
            TransplantFieldRegistry.AssertImportableModel(canonicalEnchantment,
                $"{path}.Enchantment");
            EnchantmentModel enchantment = (EnchantmentModel)canonicalEnchantment.MutableClone();
            enchantment.AssignMagnitude(item.EnchantmentAmount!.Value);
            card.AttachEnchantment(enchantment);
            enchantment.OnAttached(card);
        }
        for (int level = 0; level < item.UpgradeLevel; level++) card.Upgrade();
        return card;
    }

    internal Player CloneForCombat(
        IRunState runState,
        IDictionary<CardModel, CardModel> cardMap) =>
        CloneOwnedState(runState, cardMap, PlayerRng.CloneExact(), RelicGrabBag.Clone());

    private Player CloneOwnedState(
        IRunState runState,
        IDictionary<CardModel, CardModel> cardMap,
        PlayerRngSet clonedRng, RelicGrabBag clonedRelicBag)
    {
        // Character is a canonical ModelDb instance and therefore deliberately shared.
        var clone = new Player(Character, Creature.CurrentHp, Creature.MaxHp, MaxEnergy, Gold, UnlockState)
        {
            RunState = runState,
            IsActiveForHooks = IsActiveForHooks,
            CardRemovalsUsed = CardRemovalsUsed,
            CanUseOrRemovePotions = CanUseOrRemovePotions,
            BaseOrbSlotCount = BaseOrbSlotCount,
            PlayerRng = clonedRng,
        };
        clone.Odds = PlayerOddsSet.FromSerializable(
            Odds.ToSerializable(),
            clone.PlayerRng,
            runState.Ascension,
            new HookOddsAdapter(runState));
        clone.RelicGrabBag = clonedRelicBag;

        clone._potionSlots.Clear();
        foreach (PotionModel? potion in _potionSlots)
        {
            if (potion is null)
            {
                clone._potionSlots.Add(null);
                continue;
            }

            var clonedPotion = (PotionModel)potion.MutableClone();
            clonedPotion.AssignOwner(clone);
            clone._potionSlots.Add(clonedPotion);
        }

        foreach (RelicModel relic in _relics)
        {
            var clonedRelic = (RelicModel)relic.MutableClone();
            clonedRelic.AssignOwner(clone);
            clone._relics.Add(clonedRelic);
        }

        foreach (CardModel card in Deck.Cards)
        {
            CardModel clonedCard = card.CloneForCombat(clone);
            clone.Deck.AddInternal(clonedCard);
            cardMap.Add(card, clonedCard);
        }

        clone.PlayerCombatState = PlayerCombatState?.CloneForCombat(clone, cardMap);
        return clone;
    }
    internal Player CloneForRun(RunState runState, IDictionary<CardModel, CardModel> cardMap)
    {
        Player clone = CloneForCombat(runState, cardMap);
        clone.Creature.RestoreCumulativeHpLostFrom(Creature);
        clone.Creature.CombatId = Creature.CombatId;
        return clone;
    }

    internal Player CloneReseededForRun(RunState runState, IDictionary<CardModel, CardModel> cardMap)
    {
        PlayerRngSet fresh = PlayerRng.UsesSemanticKeys
            ? PlayerRngSet.CreateKeyed(runState.Rng.Seed) : new PlayerRngSet(runState.Rng.Seed);
        RelicGrabBag bag = RelicGrabBag.CloneReshuffled(runState.Rng.ForSemanticKey(
            Sts2Sim.Core.Entities.Rngs.RunRngType.UpFront,
            $"imagination/relic_bag/player={runState.Players.Count}"));
        Player clone = CloneOwnedState(runState, cardMap, fresh, bag);
        clone.Creature.RestoreCumulativeHpLostFrom(Creature);
        clone.Creature.CombatId = Creature.CombatId;
        return clone;
    }


    internal async Task RollbackCombatStartAsync()
    {
        foreach (PowerModel power in Creature.Powers.ToList())
        {
            try
            {
                await PowerCmd.Remove(power);
            }
            catch
            {
                // Preserve the combat-entry exception; best-effort removal continues for remaining powers.
            }
        }

        Creature.LoseBlockInternal(Creature.Block);
        Creature.CombatState = null;
        PlayerCombatState = null;
    }
}
