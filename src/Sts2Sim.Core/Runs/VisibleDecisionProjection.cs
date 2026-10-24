using System.Collections.Immutable;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Runs;

/// <summary>Copies already displayed domain values and binds the caller's original candidates.
/// Never generates stock, reward alternatives, future rooms, or replacement candidates.</summary>
public static class VisibleDecisionProjection
{
    public static VisibleDecisionSnapshot ForMap(RunState run, IReadOnlyList<MapPoint> options)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(options);
        var rows = new List<VisibleOption>();
        var bindings = new List<object?>();
        var context = new List<VisibleValue> { VisibleValue.Int("Floor", run.TotalFloor) };
        if (run.CurrentMapCoord is { } current)
        {
            context.Add(VisibleValue.Int("CurrentColumn", current.col));
            context.Add(VisibleValue.Int("CurrentRow", current.row));
        }
        if (run.CurrentActBossEncounter is { } boss)
            context.Add(VisibleValue.Id("VisibleBoss", boss.Name));
        if (run.CurrentActSecondBossEncounter is { } secondBoss)
            context.Add(VisibleValue.Id("VisibleSecondBoss", secondBoss.Name));
        var offeredCoordinates = options.Select(option => option.coord).ToHashSet();
        MapPoint[] displayOnlyPoints = run.Map.GetAllMapPoints().Append(run.Map.StartingMapPoint)
            .Append(run.Map.BossMapPoint)
            .Concat(run.Map.SecondBossMapPoint is { } second ? [second] : [])
            .Where(point => !offeredCoordinates.Contains(point.coord)).DistinctBy(point => point.coord)
            .OrderBy(point => point.coord.row).ThenBy(point => point.coord.col).ToArray();
        // Preserve every offered reference, including distinct candidates at the same coordinate.
        // Coordinate de-duplication applies only to the supplementary visible map.
        foreach (MapPoint point in options.Concat(displayOnlyPoints))
        {
            var values = new List<VisibleValue>
            {
                VisibleValue.Int("Column", point.coord.col), VisibleValue.Int("Row", point.coord.row),
                VisibleValue.Scope("PointType", point.PointType.ToString()),
                Flag("Visited", run.VisitedMapCoords.Contains(point.coord)),
            };
            foreach (MapPoint child in point.Children.OrderBy(child => child.coord.row).ThenBy(child => child.coord.col))
                values.Add(VisibleValue.Scope("Child", $"{child.coord.col},{child.coord.row}"));
            MapPoint? offered = options.FirstOrDefault(option => ReferenceEquals(option, point));
            rows.Add(new($"map:{point.coord.col}:{point.coord.row}", [.. values]) { IsEnabled = offered is not null });
            bindings.Add(offered);
        }
        return Snapshot(context, rows, bindings);
    }

    public static VisibleDecisionSnapshot ForRewards(RewardsSet rewards, IReadOnlyList<RewardDecision> candidates)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        ArgumentNullException.ThrowIfNull(candidates);
        var rows = new List<VisibleOption>();
        var bindings = new List<object?>();
        IEnumerable<Reward> displayed = new Reward?[] { rewards.Gold, rewards.Potion, rewards.Relic, rewards.Card }
            .OfType<Reward>().Concat(rewards.ExtraRewards).Where(reward => !reward.IsResolved);
        int index = 0;
        foreach (Reward reward in displayed)
        {
            rows.Add(RewardRow($"reward:{index++}", reward));
            bindings.Add(null);
        }
        foreach (RewardDecision candidate in candidates)
        {
            VisibleOption row = candidate switch
            {
                RewardDecision.TakeGold => RewardRow("take_gold", rewards.Gold),
                RewardDecision.TakePotion => RewardRow("take_potion", rewards.Potion
                    ?? throw new InvalidOperationException("Potion candidate has no offered reward.")),
                RewardDecision.TakeRelic => RewardRow("take_relic", rewards.Relic
                    ?? throw new InvalidOperationException("Relic candidate has no offered reward.")),
                RewardDecision.TakeCard take => new("take_card", []) { Contents = [VisibleContentProjection.ForCard(take.Card)] },
                RewardDecision.SkipCard => new("skip_card", [VisibleValue.Scope("Operation", "skip")]),
                RewardDecision.SelectCardAlternative alternative => new(alternative.Alternative.OptionId,
                    [VisibleValue.Id("AlternativeId", alternative.Alternative.OptionId)])
                { Contents = RewardContents(alternative.Reward) },
                RewardDecision.ResolveExtra extra => RewardRow(extra.Skip ? "skip_extra" : "resolve_extra", extra.Reward) with
                {
                    Values = RewardValues(extra.Reward).Add(Flag("Skip", extra.Skip)),
                    Contents = extra.SelectedCard is { } card ? [VisibleContentProjection.ForCard(card)] : RewardContents(extra.Reward),
                },
                RewardDecision.Done => new("done", []),
                _ => throw new NotSupportedException($"Unsupported reward decision {candidate.GetType().Name}."),
            };
            rows.Add(row);
            bindings.Add(candidate);
        }
        return Snapshot([VisibleValue.Int("PotionCapacity", rewards.Player.PotionSlots.Count),
            VisibleValue.Int("PotionSlotsAvailable", rewards.Player.PotionSlots.Count(potion => potion is null))], rows, bindings);
    }

    public static VisibleDecisionSnapshot ForShop(MerchantInventory inventory, Player player, IReadOnlyList<ShopDecision> candidates)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(candidates);
        var rows = new List<VisibleOption>();
        var bindings = new List<object?>();
        var stockRows = new Dictionary<MerchantEntry, VisibleOption>(ReferenceEqualityComparer.Instance);
        foreach (MerchantEntry entry in inventory.Cards.Cast<MerchantEntry>().Concat(inventory.Relics).Concat(inventory.Potions)
                     .Append(inventory.CardRemoval))
        {
            // Price is the existing UI-facing getter; do not recalculate or roll a base price.
            int price = entry.Price;
            bool offered = candidates.Any(candidate => candidate switch
            {
                ShopDecision.BuyCard buy => ReferenceEquals(buy.Entry, entry),
                ShopDecision.BuyRelic buy => ReferenceEquals(buy.Entry, entry),
                ShopDecision.BuyPotion buy => ReferenceEquals(buy.Entry, entry),
                ShopDecision.BuyCardRemoval => ReferenceEquals(entry, inventory.CardRemoval),
                _ => false,
            });
            VisibleContent[] contents = entry switch
            {
                MerchantCardEntry card => [VisibleContentProjection.ForCard(card.Card)],
                MerchantRelicEntry relic => [VisibleContentProjection.ForRelic(relic.Relic)],
                MerchantPotionEntry potion => [VisibleContentProjection.ForPotion(potion.Potion)],
                MerchantCardRemovalEntry => [],
                _ => throw new NotSupportedException($"Unsupported merchant entry {entry.GetType().Name}."),
            };
            var values = new List<VisibleValue> { VisibleValue.Int("Price", price), Flag("Purchased", entry.Purchased),
                Flag("Affordable", player.Gold >= price), VisibleValue.Scope("Kind", entry.GetType().Name) };
            if (entry is MerchantCardRemovalEntry)
            {
                values.Add(VisibleValue.Scope("Operation", "remove"));
                values.Add(VisibleValue.Int("Count", 1));
            }
            var row = new VisibleOption($"stock:{rows.Count}", [.. values]) { IsEnabled = offered, Contents = [.. contents] };
            stockRows.Add(entry, row);
            rows.Add(row);
            bindings.Add(null);
        }
        foreach (ShopDecision candidate in candidates)
        {
            VisibleOption row = candidate switch
            {
                ShopDecision.BuyCard buy => stockRows[buy.Entry] with { OptionKey = "buy_card" },
                ShopDecision.BuyRelic buy => stockRows[buy.Entry] with { OptionKey = "buy_relic" },
                ShopDecision.BuyPotion buy => stockRows[buy.Entry] with { OptionKey = "buy_potion" },
                ShopDecision.BuyCardRemoval remove => stockRows[inventory.CardRemoval] with
                {
                    OptionKey = "remove_card", Contents = [VisibleContentProjection.ForCard(remove.CardToRemove)],
                },
                ShopDecision.Leave => new("leave", []),
                _ => throw new NotSupportedException($"Unsupported shop decision {candidate.GetType().Name}."),
            };
            rows.Add(row);
            bindings.Add(candidate);
        }
        return Snapshot([VisibleValue.Int("Gold", player.Gold), VisibleValue.Int("PotionCapacity", player.PotionSlots.Count),
            VisibleValue.Int("PotionSlotsAvailable", player.PotionSlots.Count(potion => potion is null))], rows, bindings);
    }

    public static VisibleDecisionSnapshot ForRestSite(Player player, IReadOnlyList<RestSiteDecision> candidates)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(candidates);
        var rows = new List<VisibleOption>();
        foreach (RestSiteDecision candidate in candidates)
        {
            var values = new List<VisibleValue>();
            var contents = new List<VisibleContent>();
            switch (candidate)
            {
                case RestSiteDecision.Heal:
                    // Native HealVar displays the integer preview. This hook is pure; never invoke after-heal/reward hooks.
                    decimal preview = Hooks.Hook.ModifyRestSiteHealAmount(player.RunState, player.Creature, player.Creature.MaxHp * 0.3m);
                    values.Add(VisibleValue.Int("Heal", (int)preview));
                    values.Add(VisibleValue.Int("HealPercent", 30));
                    values.Add(VisibleValue.Id("Character", player.Character.Id.Entry));
                    foreach (RelicModel relic in player.Relics)
                    {
                        ImmutableArray<VisibleValue> extra = relic switch
                        {
                            RegalPillow => [VisibleValue.Int("Heal", 15)],
                            StoneHumidifier => [VisibleValue.Int("MaxHp", 5)],
                            TinyMailbox => [VisibleValue.Int("Count", 2), VisibleValue.Scope("RewardKind", "Potion")],
                            DreamCatcher => [VisibleValue.Scope("RewardKind", "Card")],
                            _ => [],
                        };
                        if (!extra.IsEmpty) contents.Add(new(relic.Id.Entry, extra, []));
                    }
                    break;
                case RestSiteDecision.Smith smith:
                    values.Add(VisibleValue.Scope("Operation", "upgrade"));
                    values.Add(VisibleValue.Int("Count", 1));
                    contents.Add(VisibleContentProjection.ForCard(smith.Card));
                    break;
                case RestSiteDecision.Cook:
                    values.Add(VisibleValue.Int("Cards", 2));
                    values.Add(VisibleValue.Int("MaxHp", 5));
                    break;
                case RestSiteDecision.Hatch:
                    values.Add(VisibleValue.Id("Card", "BYRDONIS_EGG"));
                    values.Add(VisibleValue.Id("Relic", "BYRDPIP"));
                    break;
                case LiftRestSiteOption:
                    values.Add(VisibleValue.Int("Strength", 1));
                    values.Add(VisibleValue.Int("LiftsLeft", 3 - player.Relics.OfType<Girya>().Single().TimesLifted));
                    break;
                case KindleRestSiteOption:
                    values.Add(VisibleValue.Int("RekindleAmount", 5));
                    values.Add(VisibleValue.Id("RelicName", "PUMPKIN_CANDLE"));
                    break;
                case CloneRestSiteOption:
                    values.Add(VisibleValue.Id("EnchantmentName", "CLONE"));
                    break;
                case DigRestSiteOption:
                    values.Add(VisibleValue.Scope("RewardKind", "Relic"));
                    break;
                default:
                    throw new NotSupportedException($"Unsupported rest-site decision {candidate.GetType().Name}.");
            }
            rows.Add(new(candidate.OptionId, [.. values]) { IsEnabled = candidate.IsEnabled, Contents = [.. contents] });
        }
        var bindings = candidates.Cast<object?>().ToList();
        if (player.RunState.CurrentRoom is RestSiteRoom room)
        {
            foreach (string optionId in room.GetDisplayedOptionIds(player))
            {
                if (candidates.Any(candidate => candidate.OptionId == optionId)) continue;
                // Only these two native families can be shown while disabled. Never regenerate
                // options or infer that an already-consumed family should still be displayed.
                if (optionId == "smith" && !player.Deck.Cards.Any(card => card.IsUpgradable))
                {
                    rows.Add(new("smith", [VisibleValue.Key("Description", "rest_site_ui:OPTION_SMITH.descriptionDisabled")])
                        { IsEnabled = false });
                    bindings.Add(null);
                }
                else if (optionId == "cook" && player.Deck.Cards.Count(card => card.IsRemovable) < 2)
                {
                    rows.Add(new("cook", [VisibleValue.Key("Description", "rest_site_ui:OPTION_COOK.descriptionDisabled"),
                        VisibleValue.Int("Cards", 2)]) { IsEnabled = false });
                    bindings.Add(null);
                }
            }
        }
        return new(new([], [.. rows])
        {
            // The frozen single-player configuration was verified to have no custom modifiers.
            // A configuration change requires qualification again; these gaps remain explicit.
            CoverageGapIds = ["G3.Modifier.NightTerrors", "G4.Multiplayer.Mend"],
        }, bindings);
    }

    public static VisibleDecisionSnapshot ForCards(CardSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Bundles is { } bundles && bundles.Count != request.Candidates.Count)
            throw new ArgumentException("Each bundle must have exactly one offered representative.", nameof(request));
        var context = new List<VisibleValue>
        {
            VisibleValue.Int("MinCount", request.MinCount), VisibleValue.Int("MaxCount", request.MaxCount),
            Flag("CanCancel", request.CanCancel), Flag("Bundle", request.IsBundleSelection),
        };
        // Source identity is technical provenance, never inferred to mean remove/upgrade/etc.
        if (request.Source is { } source) context.Add(VisibleValue.Id("SourceId", source.Id.Entry));
        var rows = new List<VisibleOption>();
        for (int i = 0; i < request.Candidates.Count; i++)
        {
            IReadOnlyList<CardModel> cards = request.Bundles is { } bundle ? bundle[i] : [request.Candidates[i]];
            rows.Add(new($"card:{i}", []) { Contents = [.. cards.Select(VisibleContentProjection.ForCard)] });
        }
        return Snapshot(context, rows, request.Candidates.Cast<object?>().ToArray());
    }

    public static VisibleDecisionSnapshot ForCustomEvent(EventModel eventModel, IReadOnlyList<CustomEventDecision> candidates)
    {
        ArgumentNullException.ThrowIfNull(eventModel);
        ArgumentNullException.ThrowIfNull(candidates);
        return eventModel.GetCustomVisibleState(candidates);
    }

    private static VisibleOption RewardRow(string key, Reward reward) =>
        new(key, RewardValues(reward)) { IsEnabled = reward is not TakeableReward takeable || takeable.CanTake,
            Contents = RewardContents(reward) };

    private static ImmutableArray<VisibleValue> RewardValues(Reward reward)
    {
        var values = new List<VisibleValue> { VisibleValue.Scope("RewardKind", reward.GetType().Name) };
        switch (reward)
        {
            case GoldReward gold: values.Add(VisibleValue.Int("Amount", gold.Amount)); break;
            case PotionReward potion:
                values.Add(Flag("CanTake", potion.CanTake));
                values.Add(Flag("Optional", potion.IsOptionalMerchantChoice));
                break;
            case CardRemovalReward:
                values.Add(VisibleValue.Scope("Operation", "remove"));
                values.Add(VisibleValue.Int("Count", 1));
                break;
        }
        return [.. values];
    }

    private static ImmutableArray<VisibleContent> RewardContents(Reward reward) => reward switch
    {
        PotionReward { Potion: { } potion } => [VisibleContentProjection.ForPotion(potion)],
        RelicReward { Relic: { } relic } => [VisibleContentProjection.ForRelic(relic)],
        SpecialCardReward special => [VisibleContentProjection.ForCard(special.Card)],
        CardReward card => [.. card.Options.Select(VisibleContentProjection.ForCard)],
        _ => [],
    };

    private static VisibleValue Flag(string name, bool value) => VisibleValue.Condition(name, value ? "true" : "false");

    private static VisibleDecisionSnapshot Snapshot(IEnumerable<VisibleValue> context, IEnumerable<VisibleOption> rows,
        IReadOnlyList<object?> bindings) => new(new([.. context], [.. rows]), bindings);
}
