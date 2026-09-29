using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Rl;

/// <summary>
/// Encodes run and combat state into protobuf-independent observation snapshots without exposing
/// draw-pile or discard-pile order.
/// </summary>
public static class ObservationEncoder
{
    public static ObservationSnapshot EncodeCardSelectionDecision(
        RunState runState,
        CardSelectionRequest request,
        IReadOnlyList<CardModel> candidates,
        int selectedCount)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentOutOfRangeException.ThrowIfNegative(selectedCount);
        if (candidates.Count > ActionSpaceLayout.MaxCardSelectionChoices)
        {
            throw new InvalidOperationException(
                $"Card selection exposes {candidates.Count} candidates, exceeding the static " +
                $"RL capacity {ActionSpaceLayout.MaxCardSelectionChoices}.");
        }

        Dictionary<CardModel, string>? bundleLabels = null;
        if (request.Bundles is { } bundles)
        {
            if (bundles.Count != request.Candidates.Count)
                throw new InvalidOperationException("Bundle selection requires one representative per bundle.");
            bundleLabels = new Dictionary<CardModel, string>(ReferenceEqualityComparer.Instance);
            for (int index = 0; index < bundles.Count; index++)
            {
                string label = "bundle:" + string.Join("+", bundles[index].Select(card => card.Id.ToString()));
                if (!bundleLabels.TryAdd(request.Candidates[index], label))
                    throw new InvalidOperationException("Bundle selection representatives must be unique instances.");
            }
        }

        var mask = new bool[ActionSpaceLayout.TotalActions];
        var slots = new List<CandidateSlot>(candidates.Count + 2);
        if (selectedCount < request.MaxCount)
        {
            for (int position = 0; position < candidates.Count; position++)
            {
                int slotIndex = ActionSpaceLayout.CardSelectionIndex(position);
                mask[slotIndex] = true;
                string label = candidates[position].Id.ToString();
                if (bundleLabels is not null && !bundleLabels.TryGetValue(candidates[position], out label!))
                    throw new InvalidOperationException("Bundle candidate is not an offered representative.");
                slots.Add(new CandidateSlot(slotIndex, label));
            }
        }
        if (selectedCount >= request.MinCount)
        {
            mask[ActionSpaceLayout.CardSelectionConfirmIndex] = true;
            slots.Add(new CandidateSlot(ActionSpaceLayout.CardSelectionConfirmIndex, "confirm_selection"));
        }
        if (request.Cancelable)
        {
            mask[ActionSpaceLayout.CardSelectionCancelIndex] = true;
            slots.Add(new CandidateSlot(ActionSpaceLayout.CardSelectionCancelIndex, "cancel_selection"));
        }
        IReadOnlyList<CardSnapshot> hand = request.Player.PlayerCombatState is { } combat
            ? combat.Hand.Cards.Select(card => EncodeCard(card, card.CanPlay(out _))).ToList()
            : Array.Empty<CardSnapshot>();
        IReadOnlyList<EnemySnapshot> enemies = request.Player.Creature.CombatState is { } state
            ? state.Enemies.Select(EncodeEnemy).ToList()
            : Array.Empty<EnemySnapshot>();

        return EncodeObservation(
            runState,
            request.Player,
            DecisionType.CardSelection,
            hand,
            enemies,
            slots,
            mask,
            request.MinCount,
            request.MaxCount,
            selectedCount,
            request.Cancelable);
    }

    public static ObservationSnapshot EncodeMapDecision(RunState runState, IReadOnlyList<MapPoint> options)
    {
        Player player = runState.Players[0];
        var mask = new bool[ActionSpaceLayout.TotalActions];
        var candidates = new List<CandidateSlot>(options.Count);

        for (int i = 0; i < options.Count && i < ActionSpaceLayout.MaxMapChoices; i++)
        {
            int slotIndex = ActionSpaceLayout.MapPointIndex(i);
            mask[slotIndex] = true;
            MapPoint option = options[i];
            candidates.Add(new CandidateSlot(slotIndex, $"{option.coord.col},{option.coord.row}:{option.PointType}"));
        }

        return EncodeObservation(
            runState,
            player,
            DecisionType.MapPoint,
            Array.Empty<CardSnapshot>(),
            Array.Empty<EnemySnapshot>(),
            candidates,
            mask);
    }

    public static ObservationSnapshot EncodeEventDecision(RunState runState, IReadOnlyList<EventOption> options)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.Count, ActionSpaceLayout.MaxEventChoices);

        Player player = runState.Players[0];
        var mask = new bool[ActionSpaceLayout.TotalActions];
        var candidates = new List<CandidateSlot>(options.Count);

        // 锁定选项必须保留槽位（位置 i 恒等于选项 i），但绝不能进合法掩码：
        // EventModel.cs:148 选到锁定选项直接抛 "Locked event options cannot be chosen."。
        // 此前这里对所有选项一律置 true，固定策略（总选第一个）碰不到，随机策略 200 局撞了 6 次。
        for (int i = 0; i < options.Count; i++)
        {
            int slotIndex = ActionSpaceLayout.EventIndex(i);
            mask[slotIndex] = options[i].IsEnabled;
            candidates.Add(new CandidateSlot(slotIndex, options[i].Key));
        }

        if (!options.Any(option => option.IsEnabled))
        {
            throw new InvalidOperationException(
                "Every event option is locked; the policy would receive an empty legal mask.");
        }

        return EncodeObservation(
            runState,
            player,
            DecisionType.Event,
            Array.Empty<CardSnapshot>(),
            Array.Empty<EnemySnapshot>(),
            candidates,
            mask);
    }

    public static ObservationSnapshot EncodeCustomEventDecision(RunState runState, EventModel @event)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(@event);
        IReadOnlyList<(CustomEventDecision Decision, string Label)> choices = BuildCustomEventCandidates(@event);
        if (choices.Count > ActionSpaceLayout.MaxCustomEventChoices)
            throw new InvalidOperationException("Custom event exposes too many candidates.");

        Player player = runState.Players[0];
        var mask = new bool[ActionSpaceLayout.TotalActions];
        var candidates = new List<CandidateSlot>(choices.Count);
        for (int position = 0; position < choices.Count; position++)
        {
            int slotIndex = ActionSpaceLayout.CustomEventIndex(position);
            mask[slotIndex] = true;
            candidates.Add(new CandidateSlot(slotIndex, choices[position].Label));
        }

        return EncodeObservation(
            runState,
            player,
            DecisionType.CustomEvent,
            Array.Empty<CardSnapshot>(),
            Array.Empty<EnemySnapshot>(),
            candidates,
            mask);
    }

    public static IReadOnlyList<(CustomEventDecision Decision, string Label)> BuildCustomEventCandidates(
        EventModel @event)
    {
        string eventId = @event.Id.ToString();
        return @event switch
        {
            CrystalSphere { Game: { } game } => Enumerable.Range(0, game.Width)
                .SelectMany(x => Enumerable.Range(0, game.Height)
                    .Where(y => game.IsHidden(x, y))
                    .Select(y => ((CustomEventDecision)new CustomEventDecision.RevealSphere(x, y),
                        $"event:{eventId}:stage:divinations-{game.DivinationCount}:reveal:{x},{y}")))
                .ToList(),
            FakeMerchant merchant => merchant.Inventory.Relics
                .Where(entry => !entry.Purchased && merchant.Owner.Gold >= entry.Price)
                .Select(entry => ((CustomEventDecision)new CustomEventDecision.BuyRelic(entry),
                    $"event:{eventId}:stage:inventory-{merchant.IsInventoryOpen}:buy_relic:{entry.Relic.Id}:{entry.Price}"))
                .Concat(merchant.Owner.PotionSlots.Where(potion => potion is FoulPotion && potion.PassesCustomUsabilityCheck)
                    .Select(potion => ((CustomEventDecision)new CustomEventDecision.UsePotion(potion!),
                        $"event:{eventId}:stage:inventory-{merchant.IsInventoryOpen}:use_potion:{potion!.Id}")))
                .Append(((CustomEventDecision)new CustomEventDecision.Leave(),
                    $"event:{eventId}:stage:inventory-{merchant.IsInventoryOpen}:leave"))
                .ToList(),
            _ => throw new InvalidOperationException(
                $"No custom interaction is available for event {@event.Id}."),
        };
    }
    public static ObservationSnapshot EncodeRewardDecision(RunState runState, RewardsSet rewards)
    {
        RewardDecisionClassification classification = RewardDecisionClassifier.Classify(rewards);
        if (classification is not RewardDecisionClassification.Choice choice)
        {
            throw new InvalidOperationException("The current reward step has no external choice.");
        }

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            choice.Candidates.Count,
            ActionSpaceLayout.MaxTotalRewardChoices);

        Player player = runState.Players[0];
        var mask = new bool[ActionSpaceLayout.TotalActions];
        var candidates = new List<CandidateSlot>(choice.Candidates.Count);

        for (int i = 0; i < choice.Candidates.Count; i++)
        {
            int slotIndex = ActionSpaceLayout.RewardIndex(i);
            mask[slotIndex] = true;
            candidates.Add(new CandidateSlot(
                slotIndex,
                EncodeRewardCandidateLabel(choice.Candidates[i])));
        }

        return EncodeObservation(
            runState,
            player,
            DecisionType.Reward,
            Array.Empty<CardSnapshot>(),
            Array.Empty<EnemySnapshot>(),
            candidates,
            mask);
    }

    public static ObservationSnapshot EncodeShopDecision(
        RunState runState,
        MerchantInventory inventory,
        Player player)
    {
        ArgumentNullException.ThrowIfNull(runState);
        IReadOnlyList<ShopDecisionCandidates.Candidate> choices =
            ShopDecisionCandidates.Build(inventory, player);
        var mask = new bool[ActionSpaceLayout.TotalActions];
        var candidates = new List<CandidateSlot>(choices.Count);

        for (int position = 0; position < choices.Count; position++)
        {
            int slotIndex = ActionSpaceLayout.ShopIndex(position);
            mask[slotIndex] = true;
            candidates.Add(new CandidateSlot(slotIndex, choices[position].Label));
        }

        return EncodeObservation(
            runState,
            player,
            DecisionType.Shop,
            Array.Empty<CardSnapshot>(),
            Array.Empty<EnemySnapshot>(),
            candidates,
            mask);
    }

    public static ObservationSnapshot EncodeRestSiteDecision(RunState runState, Player player)
    {
        ArgumentNullException.ThrowIfNull(runState);
        ArgumentNullException.ThrowIfNull(player);
        return EncodeRestSiteDecision(
            runState,
            player,
            new Rooms.RestSiteRoom().GetAvailableDecisions(runState, player));
    }

    public static ObservationSnapshot EncodeRestSiteDecision(
        RunState runState,
        Player player,
        IReadOnlyList<RestSiteDecision> decisions)
    {
        ArgumentNullException.ThrowIfNull(runState);
        IReadOnlyList<RestSiteDecisionCandidates.Candidate> choices =
            RestSiteDecisionCandidates.Build(player, decisions);
        var mask = new bool[ActionSpaceLayout.TotalActions];
        var candidates = new List<CandidateSlot>(choices.Count);

        for (int position = 0; position < choices.Count; position++)
        {
            int slotIndex = choices[position].SlotIndex;
            mask[slotIndex] = true;
            candidates.Add(new CandidateSlot(slotIndex, choices[position].Label));
        }

        return EncodeObservation(
            runState,
            player,
            DecisionType.RestSite,
            Array.Empty<CardSnapshot>(),
            Array.Empty<EnemySnapshot>(),
            candidates,
            mask);
    }

    public static ObservationSnapshot EncodeCombatDecision(RunState runState, CombatState combatState)
    {
        Player player = combatState.Players[0];
        IReadOnlyList<CardModel> hand = player.PlayerCombatState!.Hand.Cards;
        IReadOnlyList<Creature> hittableEnemies = combatState.HittableEnemies;

        var mask = new bool[ActionSpaceLayout.TotalActions];
        var candidates = new List<CandidateSlot>();
        IReadOnlyList<CombatTargetCandidates.PlayerCardTarget> playerTargets =
            CombatTargetCandidates.BuildPlayerCardTargets(combatState, player);
        if (playerTargets.Count > ActionSpaceLayout.MaxPlayerTargetChoices)
        {
            throw new InvalidOperationException(
                $"Combat exposes {playerTargets.Count} player-target card actions, exceeding " +
                $"the static RL capacity {ActionSpaceLayout.MaxPlayerTargetChoices}.");
        }

        for (int handIndex = 0; handIndex < hand.Count && handIndex < ActionSpaceLayout.MaxHand; handIndex++)
        {
            CardModel card = hand[handIndex];
            if (!card.CanPlay(out _))
            {
                continue;
            }

            if (card.TargetType == TargetType.AnyEnemy)
            {
                for (int enemyIndex = 0;
                     enemyIndex < hittableEnemies.Count && enemyIndex < ActionSpaceLayout.MaxEnemies;
                     enemyIndex++)
                {
                    int slot = ActionSpaceLayout.PlayCardIndex(handIndex, enemyIndex);
                    mask[slot] = true;
                    candidates.Add(new CandidateSlot(slot, $"play:{card.Id}->enemy{enemyIndex}"));
                }
            }
            else if (card.TargetType is not (TargetType.AnyAlly or TargetType.AnyPlayer))
            {
                int slot = ActionSpaceLayout.PlayCardIndex(handIndex, enemyIndex: 0);
                mask[slot] = true;
                candidates.Add(new CandidateSlot(slot, $"play:{card.Id}"));
            }
        }

        for (int position = 0; position < playerTargets.Count; position++)
        {
            CombatTargetCandidates.PlayerCardTarget candidate = playerTargets[position];
            int slot = ActionSpaceLayout.PlayerTargetIndex(position);
            mask[slot] = true;
            int playerIndex = combatState.Players.ToList().IndexOf(candidate.Target.Player!);
            candidates.Add(new CandidateSlot(
                slot,
                $"play:{candidate.Card.Id}->player{playerIndex}"));
        }

        mask[ActionSpaceLayout.EndTurnIndex] = true;
        candidates.Add(new CandidateSlot(ActionSpaceLayout.EndTurnIndex, "end_turn"));

        var handSnapshots = hand
            .Select(card => EncodeCard(card, card.CanPlay(out _)))
            .ToList();
        var enemySnapshots = combatState.Enemies.Select(EncodeEnemy).ToList();

        return EncodeObservation(
            runState,
            player,
            DecisionType.Combat,
            handSnapshots,
            enemySnapshots,
            candidates,
            mask);
    }

    private static PlayerSnapshot EncodePlayer(Player player) => new(
        player.Creature.CurrentHp,
        player.Creature.MaxHp,
        player.Creature.Block,
        player.PlayerCombatState?.Energy ?? 0,
        player.Gold,
        player.PotionSlots.Count);

    private static ObservationSnapshot EncodeObservation(
        RunState runState,
        Player player,
        DecisionType decisionType,
        IReadOnlyList<CardSnapshot> hand,
        IReadOnlyList<EnemySnapshot> enemies,
        IReadOnlyList<CandidateSlot> candidates,
        IReadOnlyList<bool> legalActionMask,
        int selectionMinCount = 0,
        int selectionMaxCount = 0,
        int selectionSelectedCount = 0,
        bool selectionCancelable = false) =>
        new(
            decisionType,
            EncodePlayer(player),
            hand,
            EncodeDeckView(player),
            enemies,
            EncodeMap(runState),
            player.Relics
                .Select(relic => new RelicSnapshot(relic.Id.ToString(),
                    relic is Models.Relics.PaelsWing wing ? wing.DisplayAmount : relic.StackCount))
                .ToList(),
            player.PotionSlots
                .OfType<PotionModel>()
                .Select(potion => new PotionSnapshot(potion.Id.ToString()))
                .ToList(),
            EncodeAscensionLevel(runState),
            runState.CurrentActIndex,
            candidates,
            legalActionMask,
            selectionMinCount,
            selectionMaxCount,
            selectionSelectedCount,
            selectionCancelable);

    private static int EncodeAscensionLevel(RunState runState)
    {
        int encodedLevel = 0;
        foreach (AscensionLevel level in Enum.GetValues<AscensionLevel>())
        {
            if (runState.Ascension.HasLevel(level))
            {
                encodedLevel = Math.Max(encodedLevel, (int)level);
            }
        }

        return encodedLevel;
    }

    private static string EncodeRewardCandidateLabel(RewardDecision candidate) =>
        candidate switch
        {
            RewardDecision.TakeCard takeCard => $"take_card:{takeCard.Card.Id}",
            RewardDecision.SkipCard => "skip_card",
            RewardDecision.SelectCardAlternative alternative => $"card_reward_alternative:{alternative.Alternative.OptionId}",
            RewardDecision.ResolveExtra { SelectedCard: not null } resolve =>
                $"resolve_extra_card:{resolve.SelectedCard.Id}",
            RewardDecision.ResolveExtra { Reward: Rewards.PotionReward, Skip: true } => "skip_extra_potion",
            RewardDecision.ResolveExtra { Reward: Rewards.PotionReward } => "take_extra_potion",
            RewardDecision.ResolveExtra => "skip_extra_card",
            _ => throw new InvalidOperationException(
                $"Reward decision {candidate.GetType().Name} is not an external choice."),
        };

    private static IReadOnlyList<CardSnapshot> EncodeDeckView(Player player) =>
        player.Deck.Cards
            .Select(card => EncodeCard(card, canPlay: true))
            .ToList();

    private static CardSnapshot EncodeCard(CardModel card, bool canPlay) =>
        new(
            card.Id.ToString(),
            card.EnergyCost,
            canPlay,
            card.CurrentUpgradeLevel,
            card.Type == CardType.Curse,
            card.Enchantments.SingleOrDefault()?.Id.ToString());

    private static EnemySnapshot EncodeEnemy(Creature enemy)
    {
        AbstractIntent? intent = enemy.Monster?.NextMove?.Intents.FirstOrDefault();
        return intent switch
        {
            AttackIntent attack => new EnemySnapshot(
                enemy.CurrentHp,
                enemy.MaxHp,
                enemy.Block,
                attack.IntentType.ToString(),
                attack.GetTotalDamage(enemy.CombatState!.Allies, enemy)),
            null => new EnemySnapshot(
                enemy.CurrentHp,
                enemy.MaxHp,
                enemy.Block,
                IntentType.Unknown.ToString(),
                IntentDamage: 0),
            _ => new EnemySnapshot(
                enemy.CurrentHp,
                enemy.MaxHp,
                enemy.Block,
                intent.IntentType.ToString(),
                IntentDamage: 0),
        };
    }

    private static MapSnapshot EncodeMap(RunState runState)
    {
        var nodes = runState.Map.GetAllMapPoints()
            .Select(point => new MapNodeSnapshot(
                point.coord.col,
                point.coord.row,
                point.PointType.ToString(),
                runState.VisitedMapCoords.Contains(point.coord),
                point.Children
                    .OrderBy(child => child.coord.row)
                    .Select(child => child.coord.row)
                    .ToList()))
            .ToList();

        MapCoord? current = runState.CurrentMapCoord;
        return new MapSnapshot(
            nodes,
            current?.col ?? runState.Map.StartingMapPoint.coord.col,
            current?.row ?? runState.Map.StartingMapPoint.coord.row,
            runState.VisitedMapCoords.Count);
    }
}
