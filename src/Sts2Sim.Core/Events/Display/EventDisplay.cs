using System.Globalization;
using System.Reflection;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Events.Display;

/// <summary>
/// Read-only display data of events in the shape of the game's <c>EventModel.DynamicVars</c> and <c>EventOption</c>
/// (<c>TextKey</c>, <c>IsProceed</c>, <c>Relic</c>, hover object). 偏离 #82: the simulator's events have no DynamicVars,
/// text keys or hover tips; this class reconstructs them from the simulator's visible state, fields and the game's event
/// definitions (<see cref="EventDisplayTables"/>). It changes no state and reads no RNG state: the one value the game draws
/// for display only (Punch Off's gold) is drawn from <see cref="Random.Rng.CloneExact"/> of the event RNG.
/// </summary>
public static class EventDisplay
{
    private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    /// <summary>Which rule produced a DynamicVar's value (for tests).</summary>
    internal enum VarSource { String, Special, DecimalField, Visible, Field, Default }

    // ---- DynamicVars ----

    /// <summary>
    /// The current BaseValue of each of the game event's DynamicVars, in definition order (empty when the event is not in
    /// the table). Value per var, first match wins: a simulator property with the same meaning (special cases); a decimal
    /// field of the same name (the unrounded BaseValue the visible integer rounds); the visible value of the same name; a
    /// field of the same name; the game's literal default (0 when it has none). Decimals are not rounded.
    /// </summary>
    public static IReadOnlyList<EventDynamicVar> GetDynamicVars(EventModel em) => GetDynamicVars(em, out _);

    internal static IReadOnlyList<EventDynamicVar> GetDynamicVars(EventModel em, out IReadOnlyList<VarSource> sources)
    {
        ArgumentNullException.ThrowIfNull(em);
        var res = new List<EventDynamicVar>();
        var src = new List<VarSource>();
        sources = src;
        if (!EventDisplayTables.DynamicVars.TryGetValue(em.Id.Entry, out var vars)) return res;
        VisibleDecisionMetadata? meta = null;
        try { meta = em.GetVisibleState().Metadata; }
        catch (InvalidOperationException) { }   // fail-open: no visible state; the rules below fall back to fields and defaults
        foreach (var (name, def, type) in vars)
        {
            if (type == "StringVar")
            {
                res.Add(new EventDynamicVar(name, type, 0m, IsString: true));
                src.Add(VarSource.String);
                continue;
            }
            decimal? sp = Special(em, name), dv = sp == null ? DecimalField(em, name) : null,
                vi = sp == null && dv == null ? Visible(meta, name) : null,
                fv = sp == null && dv == null && vi == null ? FieldValue(em, name) : null;
            vi = dv ?? vi;
            decimal? v = sp ?? vi ?? fv ?? def;
            src.Add(sp != null ? VarSource.Special : dv != null ? VarSource.DecimalField : vi != null ? VarSource.Visible
                : fv != null ? VarSource.Field : VarSource.Default);
            res.Add(new EventDynamicVar(name, type, v ?? 0m, IsString: false));
        }
        return res;
    }

    private static decimal? Special(EventModel em, string name) => (em, name) switch
    {
        (SlipperyBridge sb, "HpLoss") => sb.CurrentHpLoss,
        // UnrestSite.CalculateVars: Heal = missing HP at event start (HP cannot change before the choice).
        (UnrestSite, "Heal") => em.Owner.Creature.MaxHp - em.Owner.Creature.CurrentHp,
        // SpiralingWhirlpool.CalculateVars: Heal = MaxHp * 0.33, a decimal BaseValue (29.7 at 90 max HP); the visible value
        // is the rounded display.
        (SpiralingWhirlpool, "Heal") => RequiredField(em, "Heal"),
        // DenseVegetation.CalculateVars: Heal = the rest-site heal (Hook.ModifyRestSiteHealAmount of 30% max HP, a decimal),
        // set at event start and kept on later pages; the visible state shows it on the first page only.
        (DenseVegetation, "Heal") => Hooks.Hook.ModifyRestSiteHealAmount(em.Owner.RunState, em.Owner.Creature, em.Owner.Creature.MaxHp * 0.3m),
        // TabletOfTruth.Decipher sets DecipherMaxHpLoss = GetDecipherCost() for the next page; the simulator keeps the cost
        // in _currentCost.
        (_, "DecipherMaxHpLoss") when em.Id.Entry == "TABLET_OF_TRUTH" => RequiredField(em, "CurrentCost"),
        // Vakuu.CalculateVars: Visits = the save's visit count of this ancient for the character + 1; a headless run has no
        // save history (1).
        (_, "Visits") when em.Id.Entry == "VAKUU" => 1,
        // PunchOff.CalculateVars: Gold = the event Rng's NextInt(91, 99) at event start (a displayed number only). The
        // simulator's event does not draw it and uses its Rng for nothing else, so a copy of the event Rng gives the same draw.
        (_, "Gold") when em.Id.Entry == "PUNCH_OFF" => PunchOffGold(em),
        // Prickly Sponge costs 99 gold (the CanonicalVars literal is 120; the event checks 99).
        (WaterloggedScriptorium, "PricklySpongeGold") => 99,
        _ => null,
    };

    private static decimal? PunchOffGold(EventModel em)
    {
        try { return em.Rng.CloneExact().NextInt(91, 99); }
        catch (InvalidOperationException) { return null; }     // fail-open: event not begun; the literal default is used
    }

    private static decimal? Visible(VisibleDecisionMetadata? meta, string name)
    {
        if (meta == null) return null;
        foreach (var v in meta.ContextValues.Concat(meta.DisplayOptions.SelectMany(o => o.Values)))
            if (v.Name == name && v.Kind is VisibleValueKind.Integer or VisibleValueKind.DisplayDecimal
                && decimal.TryParse(v.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                return x;
        return null;
    }

    // Name match on the event's own fields (underscores and case ignored). The events have no DynamicVars of their own, so
    // a field that stores the same quantity under the game's var name is the only link; EventDisplayTests locks which vars
    // resolve this way, so a renamed field turns the test red.
    private static decimal? DecimalField(EventModel em, string name)
    {
        string want = Normalize(name);
        foreach (var f in em.GetType().GetFields(Inst | BindingFlags.DeclaredOnly))
            if (f.FieldType == typeof(decimal) && Normalize(f.Name) == want) return (decimal)f.GetValue(em)!;
        return null;
    }

    private static decimal? FieldValue(EventModel em, string name)
    {
        string want = Normalize(name);
        foreach (var f in em.GetType().GetFields(Inst | BindingFlags.DeclaredOnly))
        {
            if (Normalize(f.Name) != want) continue;
            return f.GetValue(em) switch
            {
                int i => i,
                decimal m => m,
                float fl => (decimal)(double)fl,
                double d => (decimal)d,
                _ => null,
            };
        }
        return null;
    }

    private static decimal RequiredField(EventModel em, string name) =>
        FieldValue(em, name) ?? throw new MissingFieldException(em.GetType().Name, name);

    private static string Normalize(string name) => name.Replace("_", "").ToLowerInvariant();

    // ---- options ----

    /// <summary>The display data of <paramref name="option"/>, one of <paramref name="em"/>'s current options.</summary>
    public static EventOptionDisplay DescribeOption(EventModel em, EventOption option)
    {
        ArgumentNullException.ThrowIfNull(em);
        ArgumentNullException.ThrowIfNull(option);
        VisibleOption? vis = Describe(em, option);
        RelicModel? relic = OptionRelic(em, option);
        string key = TextKey(em, option, vis, relic, out bool listed);
        return new EventOptionDisplay(key, listed, IsProceed(em, option), relic, HoverObject(em, option, vis, relic));
    }

    /// <summary>The option's visible description row, or null when the event publishes none for it.</summary>
    private static VisibleOption? Describe(EventModel em, EventOption option)
    {
        try { return em.GetVisibleState().DescribeCandidate(option); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        { return null; }   // fail-open: not a displayed option of the current page; it contributes no visible values
    }

    private static string? ValueOf(VisibleOption? v, string name) => v?.Values.FirstOrDefault(x => x.Name == name)?.Value;

    /// <summary>The game's EventOption.IsProceed: AncientEventModel's PROCEED button and Trial's DOUBLE_DOWN (the only
    /// <c>isProceed: true</c> options).</summary>
    private static bool IsProceed(EventModel em, EventOption ev) =>
        em is AncientEventModel && ev.Key == "PROCEED" || em is Trial && ev.Key == "DOUBLE_DOWN";

    /// <summary>The game's EventOption.Relic, set by EventModel/AncientEventModel.RelicOption (every Ancient relic choice and
    /// Hungry for Mushrooms): the Ancient option's offered content, or the Hungry for Mushrooms key.</summary>
    private static RelicModel? OptionRelic(EventModel em, EventOption ev)
    {
        string? entry = em switch
        {
            AncientEventModel => ev.OfferedContent?.ContentId,
            HungryForMushrooms => ev.Key,
            _ => null,
        };
        return entry == null ? null : ModelDb.GetByIdOrNull<RelicModel>(new ModelId("RELIC", entry));
    }

    /// <summary>
    /// The game's EventOption.TextKey for an option that only has a key. Rule, first match wins:
    ///  1. the option's visible DescriptionKey when the event publishes one (exact page);
    ///  2. a PROCEED button the event's listed keys do not contain is "PROCEED" (the game's literal text key for
    ///     AncientEventModel's button and Relic Trader's no-relic exit);
    ///  3. listed options of this event named exactly like the key;
    ///  4. listed (page, option) pairs whose "PAGE_OPTION" equals the key (compound keys, e.g. MERCHANT_GUILTY);
    ///  5. listed options named "{KEY}_{n}" (the game numbers repeated options, e.g. REACH_DEEPER_1/_2);
    ///  6. a relic option is "{EVENT}.pages.INITIAL.options.{RELIC}" (the game's RelicOption key);
    ///  7. otherwise "{EVENT}.pages.INITIAL.options.{KEY}".
    /// Several candidates in 3–5: the INITIAL page while the event is still on its first page (EventModel.IsOnInitialPage),
    /// otherwise the first non-INITIAL page in source order; the first candidate when the preferred kind is absent.
    /// </summary>
    private static string TextKey(EventModel em, EventOption ev, VisibleOption? vis, RelicModel? relic, out bool listed)
    {
        string id = em.Id.Entry;
        listed = true;
        if (ValueOf(vis, "DescriptionKey") is { } dk && dk.StartsWith(id + ".pages.", StringComparison.Ordinal)
            && dk.EndsWith(".description", StringComparison.Ordinal))
            return dk[..^".description".Length];
        EventDisplayTables.OptionKeys.TryGetValue(id, out var opts);
        if (ev.Key == "PROCEED" && !(opts != null && opts.Any(o => o.Option == "PROCEED")))
        { listed = false; return "PROCEED"; }
        if (opts != null)
        {
            string? pick =
                Choose(opts.Where(o => o.Option == ev.Key).ToList(), em) ??
                Choose(opts.Where(o => o.Page + "_" + o.Option == ev.Key).ToList(), em) ??
                Choose(opts.Where(o => o.Option.StartsWith(ev.Key + "_", StringComparison.Ordinal)
                    && o.Option[(ev.Key.Length + 1)..].All(char.IsDigit)).ToList(), em);
            if (pick != null) return $"{id}.pages.{pick}";
        }
        listed = false;
        if (relic != null) return $"{id}.pages.INITIAL.options.{relic.Id.Entry}";
        return $"{id}.pages.INITIAL.options.{ev.Key}";
    }

    private static string? Choose(List<(string Page, string Option)> c, EventModel em)
    {
        if (c.Count == 0) return null;
        var pick = c.Count == 1 ? c[0]
            : em.IsOnInitialPage ? (c.Any(x => x.Page == "INITIAL") ? c.First(x => x.Page == "INITIAL") : c[0])
            : (c.Any(x => x.Page != "INITIAL") ? c.First(x => x.Page != "INITIAL") : c[0]);
        return $"{pick.Page}.options.{pick.Option}";
    }

    // ---- hover object ----

    /// <summary>
    /// The game's option hover object: the first card or potion, or relic other than the option's own relic, among the
    /// option's hover tips. The simulator's options carry no hover tips; the option's visible description lists the
    /// offered content (VisibleOption.Contents, category from its coverage id) in display order and is used instead. Cards
    /// are built from the canonical model at the shown upgrade level (a copy; the game's hover card is the offered card).
    /// </summary>
    private static AbstractModel? HoverObject(EventModel ev, EventOption opt, VisibleOption? vis, RelicModel? own)
    {
        // Slippery Bridge "Overcome": the hover card is the deck card that would be lost, the instance itself.
        if (ev is SlipperyBridge sb && opt.Key == "OVERCOME")
        {
            try { return sb.RandomCardToLose; }
            catch (InvalidOperationException) { }   // fail-open: event not begun; falls through to the visible description
        }
        // Options whose hover tips (the game's EventOption constructor arguments) differ from what the visible description
        // lists: Tea Master's tea options pass FromRelicExcludingItself<tea>() (the tea's own extra tips; the option carries
        // no relic), Lost Wisp's Claim lists FromRelic<LostWisp>() before the Decay card, The Lantern Key's options have none;
        // Wuppy in Trouble's Help Immediately adds FromCardWithCardHoverTips<Injury>(), its other tips and the two Thermae
        // baths carry no model.
        switch (ev.Id.Entry, opt.Key)
        {
            case ("TEA_MASTER", "BONE_TEA" or "EMBER_TEA" or "TEA_OF_DISCOURTESY"): return RelicTipObject(opt.Key);
            case ("LOST_WISP", "CLAIM"): return ModelDb.GetByIdOrNull<RelicModel>(new ModelId("RELIC", "LOST_WISP"));
            case ("THE_LANTERN_KEY", _): return null;
            case ("WUPPY_IN_TROUBLE", "HELP_IMMEDIATELY"): return ModelDb.Card<Injury>();
            case ("WUPPY_IN_TROUBLE", "BE_CAUTIOUS"): return null;
            case ("THERMAE_NONUPEIPE", "THERMAL_BATHS" or "PRIVATE_OASIS"): return null;
            case ("TINKER_TIME", "CHOOSE_CARD_TYPE"): return null;
            case ("TINKER_TIME", _) when ev is TinkerTime tt:
            {
                // TinkerTime.GetCardTypeHoverTip / GetRiderHoverTip: a Mad Science created for the owner with the option's
                // type (rider None), or with the chosen type and the option's rider.
                MadScienceRider rider;
                if (Enum.TryParse(opt.Key, true, out CardType type)) rider = MadScienceRider.None;
                else
                {
                    type = (CardType)(typeof(TinkerTime).GetField("_chosenType", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? throw new MissingFieldException(nameof(TinkerTime), "_chosenType")).GetValue(tt)!;
                    rider = Enum.Parse<MadScienceRider>(opt.Key, true);
                }
                var ms = (MadScience)ModelDb.Card<MadScience>().MutableClone();
                ms.AssignOwner(ev.Owner);
                ms.Configure(type, rider);
                return ms;
            }
        }
        if (vis == null) return null;
        // Dusty Tome: its extra tips are its ancient card's tips followed by HoverTipFactory.FromCard(card, upgrade: true), an
        // upgraded copy (DustyTome.SetupForPlayer).
        if (own?.Id.Entry == "DUSTY_TOME" && vis.Contents.SelectMany(c => c.Values).FirstOrDefault(v => v.Name == "AncientCard")?.Value is { } ac
            && ModelDb.GetByIdOrNull<CardModel>(new ModelId("CARD", ac)) is { } tomeCard)
        {
            var up = (CardModel)tomeCard.MutableClone();
            if (up.IsUpgradable) up.Upgrade();
            return up;
        }
        static IEnumerable<VisibleContent> Flat(IEnumerable<VisibleContent> xs) => xs.SelectMany(x => new[] { x }.Concat(Flat(x.Children)));
        // Pre-order over the contents and their children: an Ancient relic option's hover tips are the relic's own tips
        // (HoverTipsExcludingRelic), which the visible projection lists as that relic content's children.
        foreach (var c in Flat(vis.Contents))
        {
            string cat = c.CoverageGapIds.FirstOrDefault(g => g.StartsWith("G2.Canonical.", StringComparison.Ordinal)) is { } g
                ? g["G2.Canonical.".Length..].Split(':')[0] : "";
            switch (cat)
            {
                case "Card" when ModelDb.GetByIdOrNull<CardModel>(new ModelId("CARD", c.ContentId)) is { } card:
                {
                    int up = int.TryParse(c.Values.FirstOrDefault(v => v.Name == "UpgradeLevel")?.Value, out var u) ? u : 0;
                    var copy = (CardModel)card.MutableClone();
                    for (int i = 0; i < up && copy.IsUpgradable; i++) copy.Upgrade();
                    return copy;
                }
                case "Potion" when ModelDb.GetByIdOrNull<PotionModel>(new ModelId("POTION", c.ContentId)) is { } pot:
                    return pot;
                case "Relic" when ModelDb.GetByIdOrNull<RelicModel>(new ModelId("RELIC", c.ContentId)) is { } rel && (own == null || rel.Id != own.Id):
                    return rel;
            }
        }
        // The option's relic carries a card or potion in its own hover tips (the game's RelicModel.HoverTipsExcludingRelic)
        // that the visible projection does not list.
        if (own != null && RelicHoverObject.ContainsKey(own.Id.Entry)) return RelicTipObject(own.Id.Entry);
        return null;
    }

    /// <summary>The first card or potion among a relic's extra hover tips (<see cref="RelicHoverObject"/>), or null.</summary>
    private static AbstractModel? RelicTipObject(string relicEntry)
    {
        if (!RelicHoverObject.TryGetValue(relicEntry, out var ho)) return null;
        return ho.Potion ? ModelDb.GetByIdOrNull<PotionModel>(new ModelId("POTION", ho.Id))
            : ModelDb.GetByIdOrNull<CardModel>(new ModelId("CARD", ho.Id)) is { } hc ? (CardModel)hc.MutableClone() : null;
    }

    // The first card or potion among each relic's ExtraHoverTips in the game's relics (HoverTipFactory.FromCard<T>,
    // FromCardWithCardHoverTips<T>, FromPotion<T>; energy, keyword and power tips carry no model). Relics with no such tip,
    // and the ones whose tips are built at run time (Dusty Tome, handled above; Archaic Tooth, Touch of Orobas), are not
    // listed.
    internal static readonly IReadOnlyDictionary<string, (string Id, bool Potion)> RelicHoverObject =
        new Dictionary<string, (string, bool)>(StringComparer.Ordinal)
        {
            ["BIIIG_HUG"] = ("SOOT", false),
            ["BIZARRE_LAMP"] = ("JOY", false),
            ["BLESSED_ANTLER"] = ("DAZED", false),
            ["BLOOD_SOAKED_ROSE"] = ("ENTHRALLED", false),
            ["BYRDINE"] = ("BYRD_SWOOP", false),
            ["BYRDPIP"] = ("BYRD_SWOOP", false),
            ["CALLING_BELL"] = ("CURSE_OF_THE_BELL", false),
            ["CLAWS"] = ("MAUL", false),
            ["CURSED_PEARL"] = ("GREED", false),
            ["DISTINGUISHED_CAPE"] = ("APPARITION", false),
            ["DOWSING_ROD"] = ("DOWSING", false),
            ["FERTILIZER"] = ("SADNESS", false),
            ["FUNERARY_MASK"] = ("SOUL", false),
            ["HEFTY_TABLET"] = ("INJURY", false),
            ["HELICAL_DART"] = ("SHIV", false),
            ["JEWELRY_BOX"] = ("APOTHEOSIS", false),
            ["NEOWS_SACRIFICE"] = ("AMBERGRIS", true),
            ["NEOWS_TORMENT"] = ("NEOWS_FURY", false),
            ["NINJA_SCROLL"] = ("SHIV", false),
            ["PAELS_HORN"] = ("RELAX", false),
            ["PETRIFIED_TOAD"] = ("POTION_SHAPED_ROCK", true),
            ["PRESERVED_FOG"] = ("FOLLY", false),
            ["RADIANT_PEARL"] = ("LUMINESCE", false),
            ["SERE_TALON"] = ("WISH", false),
            ["STORYBOOK"] = ("BRIGHTEST_FLAME", false),
            ["TANXS_WHISTLE"] = ("WHISTLE", false),
            ["TEA_OF_DISCOURTESY"] = ("DAZED", false),
        };
}
