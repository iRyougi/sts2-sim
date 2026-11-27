using Sts2Sim.Core.Models.Afflictions;
using System.Collections.Immutable;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models.Enchantments;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Relics;

namespace Sts2Sim.Core.Runs;

/// <summary>Source-audited UI projections only. Never enumerate model fields or transplant properties.</summary>
public static partial class VisibleContentProjection
{
    private static readonly ConcurrentDictionary<(Type Type, string Name), FieldInfo> Fields = new();
    private static readonly ConcurrentDictionary<(string Id, int Upgrade), double> DefaultDamage = new();
    private static T Read<T>(object model, string name) => (T)Fields.GetOrAdd((model.GetType(), name), key =>
        key.Type.GetField(key.Name, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidDataException($"Missing audited field {key.Type.Name}.{key.Name}.")).GetValue(model)!;

    public static LegacyVisibleCard LegacyCard(CardModel card)
    {
        var deltas = new List<LegacyVisibleNumericDelta>();
        var categories = new List<LegacyVisibleCategory>();
        // Schema-2 compatibility: GeneticAlgorithm intentionally retains its historical null delta.
        if (card is TheScythe scythe)
        {
            double baseline = DefaultDamage.GetOrAdd((card.Id.Entry, card.CurrentUpgradeLevel), key =>
            {
                var clean = (CardModel)ModelDb.GetById<CardModel>(card.Id).MutableClone();
                for (int i = 0; i < key.Upgrade; i++) clean.Upgrade();
                return ((ICardChoiceBaseValueProvider)clean).CardChoiceBaseValues!.Value.Damage;
            });
            deltas.Add(new("Damage", ((ICardChoiceBaseValueProvider)scythe).CardChoiceBaseValues!.Value.Damage - baseline));
        }
        if (card is Dowsing dowsing) deltas.Add(new("RoomsRemaining", dowsing.RoomsRemaining - Dowsing.MaxRooms));
        if (card is Guilty) deltas.Add(new("CombatsRemaining", -Read<int>(card, "_combatsCompleted")));
        if (card is MadScience mad)
        {
            categories.Add(new("Type", mad.Type.ToString())); categories.Add(new("Rider", mad.Rider.ToString()));
        }
        return new(card.Id.Entry, card.CurrentUpgradeLevel, card.Enchantments.Select(e => e.Id.Entry).ToArray())
        {
            EnchantmentInstances = card.Enchantments.Select(enchantment => new LegacyVisibleEnchantment(enchantment.Id.Entry,
                enchantment.GetType().Name is "Nimble" or "Sharp" or "Swift" or "Vigorous" or "Goopy" or "Sown" ? Amount(enchantment) : null,
                enchantment.Status.ToString())).ToArray(),
            NumericDeltas = card is GeneticAlgorithm ? null : deltas.ToArray(), InstanceCategories = categories.ToArray()
        };
    }
    private static double Amount(EnchantmentModel enchantment)
    {
        decimal amount = enchantment.Magnitude;
        if (amount != decimal.Truncate(amount) || amount is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException("Visible enchantment amount is not representable by the native UI.");
        return (double)amount;
    }
    public static LegacyVisibleRelic LegacyRelic(RelicModel relic)
    {
        // Numbers below transcribe DisplayAmount, not learned thresholds or strategy weights.
        double? amount = relic switch
        {
            PenNib => Read<int>(relic, "_attacksPlayed") % 10,
            Nunchaku => Read<int>(relic, "_attacksPlayed") % 10,
            HappyFlower => Read<int>(relic, "_turnCounter"),
            FakeHappyFlower => Read<int>(relic, "_turnsSeen"),
            BookOfFiveRings rings => rings.DisplayAmount,
            FishingRod rod => rod.CombatsSeen % 3,
            JossPaper => Read<int>(relic, "_cardsExhausted"),
            LastingCandy => Read<int>(relic, "_combatRewardsSeen") % 2,
            PaelsWing wing => wing.DisplayAmount,
            Pendulum => Read<int>(relic, "_turnCounter"),
            PollinousCore => Read<int>(relic, "_handsSeen"),
            TuningFork => Read<int>(relic, "_skillsPlayed"),
            Girya girya => girya.TimesLifted,
            SwordOfStone => Read<int>(relic, "_eliteVictories"),
            EmberTea tea => tea.CombatsLeft,
            BoneTea tea => tea.CombatsLeft, // Confirmed Combats placeholder in the native hover text.
            PumpkinCandle candle => candle.KindleCount,
            WingedBoots boots when !boots.IsUsedUp => 3 - boots.TimesUsed,
            ToyBox toy when !toy.IsUsedUp => toy.DisplayAmount,
            SilverCrucible crucible when crucible.TimesUsed < 3 => 3 - crucible.TimesUsed,
            PaelsTooth => ((ICollection)Read<object>(relic, "_removedCards")).Count is var count && count > 0 ? count : null,
            _ => null
        };
        var states = new List<string> { relic.IsWax ? "wax" : "not-wax", relic.IsMelted ? "melted" : "not-melted" };
        // RelicModel.Status (NRelicInventoryHolder pulse). Tea sets keep their historical explicit "normal" value.
        if (relic is VenerableTeaSet or FakeVenerableTeaSet) states.Add(relic.Status == RelicStatus.Active ? "active" : "normal");
        else if (relic.Status == RelicStatus.Active) states.Add("active");
        if (relic.IsUsedUp || relic.IsMelted || relic is PumpkinCandle { KindleCount: <= 0 }) states.Add("disabled");
        return new(relic.Id.Entry) { CounterVisible = relic is GalacticDust or IronClub or WongosMysteryTicket ? null : amount.HasValue, CounterAmount = amount, VisibleStates = states.ToArray() };
    }
}



public static partial class VisibleContentProjection
{
    private static readonly ConcurrentDictionary<string, ImmutableArray<string>> CardGapIds = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ImmutableArray<string>> RelicGapIds = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ImmutableArray<string>> PotionGapIds = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ImmutableArray<string>> EnchantmentGapIds = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, ImmutableArray<string>> AfflictionGapIds = new(StringComparer.Ordinal);

    private static string CardTypeName(CardType type) => type switch
    {
        CardType.Attack => "Attack",
        CardType.Skill => "Skill",
        CardType.Power => "Power",
        CardType.Status => "Status",
        CardType.Curse => "Curse",
        _ => type.ToString(),
    };

    private static string CardRarityName(CardRarity rarity) => rarity switch
    {
        CardRarity.None => "None",
        CardRarity.Basic => "Basic",
        CardRarity.Common => "Common",
        CardRarity.Uncommon => "Uncommon",
        CardRarity.Rare => "Rare",
        CardRarity.Ancient => "Ancient",
        CardRarity.Event => "Event",
        CardRarity.Token => "Token",
        CardRarity.Status => "Status",
        CardRarity.Curse => "Curse",
        CardRarity.Quest => "Quest",
        _ => rarity.ToString(),
    };

    private static string TargetTypeName(TargetType targetType) => targetType switch
    {
        TargetType.None => "None",
        TargetType.Self => "Self",
        TargetType.AnyEnemy => "AnyEnemy",
        TargetType.AllEnemies => "AllEnemies",
        TargetType.RandomEnemy => "RandomEnemy",
        TargetType.AnyAlly => "AnyAlly",
        _ => targetType.ToString(),
    };

    private static string KeywordName(CardKeyword keyword) => keyword switch
    {
        CardKeyword.Exhaust => "Exhaust",
        CardKeyword.Ethereal => "Ethereal",
        CardKeyword.Innate => "Innate",
        CardKeyword.Retain => "Retain",
        CardKeyword.Unplayable => "Unplayable",
        CardKeyword.Eternal => "Eternal",
        CardKeyword.Sly => "Sly",
        _ => keyword.ToString(),
    };

    /// <summary>Current visible card state, independent of whether the card was fixed or randomly offered.</summary>
    public static VisibleContent ForCard(CardModel card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var values = ImmutableArray.CreateBuilder<VisibleValue>(12);
        values.Add(VisibleValue.Int("UpgradeLevel", card.CurrentUpgradeLevel));
        values.Add(VisibleValue.Scope("Type", CardTypeName(card.Type)));
        values.Add(VisibleValue.Scope("Rarity", CardRarityName(card.Rarity)));
        values.Add(VisibleValue.Scope("TargetType", TargetTypeName(card.TargetType)));
        AddCost(values, "EnergyCost", card.EnergyCost, card.CostsXEnergy);
        AddCost(values, "StarCost", card.StarCost, card.CostsXStar, absent: !card.HasStarCost);
        IReadOnlyCollection<CardKeyword> keywords = card.Keywords;
        if (keywords.Count == 1)
        {
            foreach (CardKeyword kw in keywords) values.Add(VisibleValue.Scope("Keyword", KeywordName(kw)));
        }
        else if (keywords.Count > 1)
        {
            foreach (CardKeyword keyword in keywords.OrderBy(static keyword => keyword))
                values.Add(VisibleValue.Scope("Keyword", KeywordName(keyword)));
        }

        // Native card hover text adds ReplayDynamic from the enchanted replay count.
        int replayCount = card.BaseReplayCount;
        if (card.EnchantmentList is { Count: > 0 } replayEnchs)
        {
            for (int i = 0; i < replayEnchs.Count; i++)
            {
                EnchantmentModel enchantment = replayEnchs[i];
                if (enchantment is Spiral || enchantment is Glam { Status: EnchantmentStatus.Normal })
                    replayCount++;
            }
        }
        if (replayCount > 0) values.Add(VisibleValue.Int("Replay", replayCount));

        // 41cef1ea mutation audit: every Core Cards field assignment outside upgrades,
        // cloning and constructors was compared with native DynamicVars/description use.
        // Damage changes: Claw, KinglyPunch, Maul, Rampage, SovereignBlade, TheBall,
        // TheScythe and Thrash. Persistent Block: GeneticAlgorithm. Quest countdowns:
        // Dowsing/Guilty. Configured descriptions: MadScience/Disintegration/Wither.
        // Abundance candidates, Bolas/ThrummingHatchet/Fetch turn stamps, Bombardment's
        // replay guard, TheHunt pending rewards and Regret's settlement scratch are not
        // displayed description values. SpoilsMap's map target is not a card placeholder.
        if (card is Claw or KinglyPunch or Maul or Rampage or SovereignBlade or TheBall or TheScythe or Thrash)
        {
            if (card is not ICardDamageVariableProvider provider || !provider.TryGetThrashDamageVariable(out decimal damage))
                throw new InvalidDataException($"Missing audited visible damage for {card.Id.Entry}.");
            values.Add(VisibleValue.Number("Damage", damage));
        }
        switch (card)
        {
            case GeneticAlgorithm genetic:
                values.Add(VisibleValue.Int("Block", genetic.CurrentBlock));
                values.Add(VisibleValue.Int("Increase", Read<int>(genetic, "_increase")));
                break;
            case TheScythe:
                values.Add(VisibleValue.Int("Increase", card.IsUpgraded ? 7 : 5));
                break;
            case Claw:
                values.Add(VisibleValue.Int("Increase", card.IsUpgraded ? 3 : 2));
                break;
            case KinglyPunch:
                values.Add(VisibleValue.Int("Increase", card.IsUpgraded ? 6 : 4));
                break;
            case Maul:
                values.Add(VisibleValue.Int("Increase", card.IsUpgraded ? 3 : 2));
                break;
            case Rampage:
                values.Add(VisibleValue.Int("Increase", card.IsUpgraded ? 10 : 5));
                break;
            case TheBall:
                values.Add(VisibleValue.Int("Increase", card.IsUpgraded ? 15 : 10));
                break;
            case Dowsing dowsing:
                values.Add(VisibleValue.Int("Rooms", dowsing.RoomsRemaining));
                break;
            case Guilty:
                values.Add(VisibleValue.Int("Combats", 5 - Read<int>(card, "_combatsCompleted")));
                break;
            case MadScience mad:
                AddMadScience(values, mad);
                break;
            case Wither wither:
                values.Add(VisibleValue.Int("Damage", wither.TurnEndDamage));
                values.Add(VisibleValue.Int("FakeUpgradeLevel", wither.FakeUpgradeLevel));
                break;
            case Disintegration disintegration:
                values.Add(VisibleValue.Int("DisintegrationPower", disintegration.PowerAmount));
                break;
            case ShowOff showOff:
                values.Add(VisibleValue.Int("AimPower", showOff.CurrentAim));
                values.Add(VisibleValue.Int("ShowOffPower", card.IsUpgraded ? 3 : 2));
                break;
        }
        // Hand glow as the player sees it (NHandCardHolder.ShouldGlowGold / ShouldGlowRed): gold needs
        // CanPlay() and the play phase, red only the play phase; UpdateCard computes neither unless
        // CombatManager.IsInProgress. Cards outside a live hand get no glow values.
        if (card.HasOwner && card.Pile?.Type == PileType.Hand && card.Owner.PlayerCombatState is { } glowCombat)
        {
            bool glowActive = glowCombat.Phase == PlayerTurnPhase.Play && card.Owner.Creature.CombatState?.IsLiveCombat() == true;
            values.Add(VisibleValue.Int("GlowGold", glowActive && card.CanPlay(out _) && card.ShouldGlowGold ? 1 : 0));
            values.Add(VisibleValue.Int("GlowRed", glowActive && card.ShouldGlowRed ? 1 : 0));
        }
        ImmutableArray<VisibleContent> children = card.EnchantmentList is { Count: > 0 } enchList
            ? enchList.Select(ForEnchantment).ToImmutableArray()
            : ImmutableArray<VisibleContent>.Empty;
        if (card.Affliction is { } affliction)
            children = children.Add(new VisibleContent(affliction.Id.Entry,
                affliction is Entangled or Galvanized or Tainted
                    ? [VisibleValue.Int("Amount", affliction.Amount)] : [], [])
            {
                CoverageGapIds = AfflictionGapIds.GetOrAdd(affliction.Id.Entry, static entry => [$"G2.Canonical.Affliction:{entry}"])
            });
        return new(card.Id.Entry, values.ToImmutable(), children)
        {
            CoverageGapIds = CardGapIds.GetOrAdd(card.Id.Entry, static entry => [$"G2.Canonical.Card:{entry}"])
        };
    }

    private static void AddCost(ImmutableArray<VisibleValue>.Builder values, string name, int amount, bool costsX, bool absent = false)
    {
        if (costsX) values.Add(VisibleValue.Scope(name, "X"));
        else if (absent) values.Add(VisibleValue.Scope(name, "Absent"));
        else if (amount < 0) values.Add(VisibleValue.Scope(name, "Unplayable"));
        else values.Add(VisibleValue.Int(name, amount));
    }

    private static void AddMadScience(ImmutableArray<VisibleValue>.Builder values, MadScience mad)
    {
        values.Add(VisibleValue.Scope("Rider", mad.Rider.ToString()));
        if (mad.Type == CardType.Attack) values.Add(VisibleValue.Int("Damage", 12));
        if (mad.Type == CardType.Skill) values.Add(VisibleValue.Int("Block", 8));
        switch (mad.Rider)
        {
            case MadScienceRider.Sapping:
                values.Add(VisibleValue.Int("SappingWeak", 2));
                values.Add(VisibleValue.Int("SappingVulnerable", 2));
                break;
            case MadScienceRider.Violence: values.Add(VisibleValue.Int("ViolenceHits", 3)); break;
            case MadScienceRider.Choking: values.Add(VisibleValue.Int("ChokingDamage", 6)); break;
            case MadScienceRider.Energized: values.Add(VisibleValue.Int("EnergizedEnergy", 2)); break;
            case MadScienceRider.Wisdom: values.Add(VisibleValue.Int("WisdomCards", 3)); break;
            case MadScienceRider.Expertise:
                values.Add(VisibleValue.Int("ExpertiseStrength", 2));
                values.Add(VisibleValue.Int("ExpertiseDexterity", 2));
                break;
            case MadScienceRider.Curious: values.Add(VisibleValue.Int("CuriousReduction", 1)); break;
        }
    }

    /// <summary>Native badge, description parameters and visible instance status only.</summary>
    public static VisibleContent ForRelic(RelicModel relic)
    {
        ArgumentNullException.ThrowIfNull(relic);
        LegacyVisibleRelic legacy = LegacyRelic(relic);
        var values = ImmutableArray.CreateBuilder<VisibleValue>();
        values.Add(VisibleValue.Scope("Rarity", relic.Rarity.ToString()));
        foreach (string state in legacy.VisibleStates ?? [])
            values.Add(VisibleValue.Condition("State", state));
        double? badge = legacy.CounterAmount;
        // Native ShowCounter=false: DisplayAmount alone does not authorize a badge.
        if (relic is BoneTea) badge = null;
        if (relic is EmberTea tea) badge = Math.Max(0, tea.CombatsLeft);
        if (relic is PaelsLegion)
        {
            int cooldown = Read<int>(relic, "_cooldown");
            badge = IsInCombat(relic) && !relic.IsCanonical && cooldown > 0 ? cooldown : null;
        }
        if (relic is StoneCalendar)
        {
            int turn = relic.Owner?.PlayerCombatState?.TurnNumber ?? 0;
            badge = IsInCombat(relic) && !relic.IsCanonical && turn < 7 ? turn : null;
        }
        if (relic is GalacticDust) badge = Read<int>(relic, "_starsSpent") % 10;
        if (relic is IronClub) badge = Read<int>(relic, "_cardsPlayed") % 4;
        if (relic is WongosMysteryTicket ticket)
            badge = 5 - ticket.CombatsFinished is var remaining && remaining > 0 ? remaining : null;
        if (badge.HasValue)
            values.Add(VisibleValue.Int("DisplayAmount", checked((int)badge.Value)));
        values.Add(VisibleValue.Condition("ShowCounter", badge.HasValue ? "true" : "false"));
        // BoneTea's hover text has a Combats placeholder even though it has no badge.
        if (relic is BoneTea boneTea)
            values.Add(VisibleValue.Int("Combats", Math.Max(0, boneTea.CombatsLeft)));
        if (relic is WongosMysteryTicket mysteryTicket)
            values.Add(VisibleValue.Int("RemainingCombats", mysteryTicket.RemainingCombats));
        return new(relic.Id.Entry, values.ToImmutable(), [])
        {
            CoverageGapIds = RelicGapIds.GetOrAdd(relic.Id.Entry, static entry => [$"G2.Canonical.Relic:{entry}"])
        };
    }

    private static bool IsInCombat(RelicModel relic) =>
        relic.Owner?.Creature.CombatState?.IsLiveCombat() == true;

    public static VisibleContent ForPotion(PotionModel potion)
    {
        ArgumentNullException.ThrowIfNull(potion);
        return new(potion.Id.Entry,
            [VisibleValue.Scope("Rarity", potion.Rarity.ToString()),
             VisibleValue.Scope("TargetType", TargetTypeName(potion.TargetType))], [])
        {
            CoverageGapIds = PotionGapIds.GetOrAdd(potion.Id.Entry, static entry => [$"G2.Canonical.Potion:{entry}"])
        };
    }

    public static VisibleContent ForEnchantment(EnchantmentModel enchantment)
    {
        ArgumentNullException.ThrowIfNull(enchantment);
        var values = ImmutableArray.CreateBuilder<VisibleValue>();
        values.Add(VisibleValue.Condition("Status", enchantment.Status.ToString()));
        bool showAmount = enchantment is Nimble or Sharp or Swift or Vigorous or Adroit or Momentum;
        values.Add(VisibleValue.Condition("ShowAmount", showAmount ? "true" : "false"));
        if (showAmount)
            values.Add(VisibleValue.Int("DisplayAmount", checked((int)Amount(enchantment))));
        if (enchantment is Nimble or Sharp or Swift or Vigorous or Momentum or Sown)
            values.Add(VisibleValue.Int("Amount", checked((int)Amount(enchantment))));
        if (enchantment is Adroit)
            values.Add(VisibleValue.Int("Block", checked((int)Amount(enchantment))));
        if (enchantment is Inky)
            values.Add(VisibleValue.Int("WeakPower", 1));
        // Momentum/Goopy change current card damage/block. Their current accumulated
        // amount is visible on the card, although it is not an enchantment badge.
        if (enchantment is Momentum)
            values.Add(VisibleValue.Int("DamageAdditive", Read<int>(enchantment, "_extraDamage")));
        if (enchantment is Goopy)
            values.Add(VisibleValue.Int("BlockAdditive", checked((int)Amount(enchantment) - 1)));
        return new(enchantment.Id.Entry, values.ToImmutable(), [])
        {
            CoverageGapIds = EnchantmentGapIds.GetOrAdd(enchantment.Id.Entry, static entry => [$"G2.Canonical.Enchantment:{entry}"])
        };
    }
}
