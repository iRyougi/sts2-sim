using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Afflictions;
using Sts2Sim.Core.Combat.StateDescription;

namespace Sts2Sim.Core.Models;

/// <summary>
/// 卡牌基类。偏离 #32：无完整 <c>CardEnergyCost</c> 修饰符叠加链（Replay 部分已由
/// <see cref="BaseReplayCount"/> 实现，见偏离 #105）；Plan06c Task 8 仅补齐精确的
/// <see cref="TemporaryFreeThisTurn"/> 窄化能力。<see cref="EnergyCost"/> 是解析好的费用；
/// X 费由资源快照结算。偏离 #33：HasUnplayableKeyword 已在 Plan06e 落地，
/// BlockedByCardLogic 的正式门禁在 Plan08b-3d 补齐。
/// </summary>
public abstract class CardModel : AbstractModel, ICombatStateDescriptionContributor
{
    public abstract CardType Type { get; }

    public abstract CardRarity Rarity { get; }

    public abstract TargetType TargetType { get; }

    public virtual bool IsColorless => false;

    /// <summary>Whether the real card has <c>MultiplayerConstraint.MultiplayerOnly</c>.</summary>
    public virtual bool IsMultiplayerOnly => false;

    /// <summary>Whether this card is eligible for random generation by modifiers and rewards.</summary>
    public virtual bool CanBeGeneratedByModifiers => true;

    /// <summary>Whether this card is eligible for generated-card effects.</summary>
    public virtual bool CanBeGeneratedInCombat => true;

    /// <summary>Whether the card's own rules can grant Block.</summary>
    public virtual bool GainsBlock => false;

    protected abstract int CanonicalEnergyCost { get; }

    /// <summary>The unmodified source cost used by effects that must ignore temporary combat costs.</summary>
    public int CanonicalEnergyCostValue => CanonicalEnergyCost;

    protected virtual int CanonicalStarCost => -1;

    protected virtual bool IsPlayable => true;

    protected virtual bool IsXEnergyCost => false;

    protected virtual bool IsXStarCost => false;

    private int _energyCostUpgradeDelta;
    private int _energyCostThisCombatDelta;
    private int _energyCostThisTurnDelta;
    private List<LocalEnergyModifier> _localEnergyModifiers = [];

    private enum LocalEnergyModifierKind
    {
        FreeThisTurn,
        FreeUntilPlayed,
        FreeThisCombat,
        SetThisTurn,
        SetThisTurnOrUntilPlayed,
        SetThisCombat,
        AddThisCombat,
        AddThisTurn,
        AddUntilPlayed,
    }

    private readonly record struct LocalEnergyModifier(LocalEnergyModifierKind Kind, int Amount)
    {
        public bool IsAbsolute => Kind is
            LocalEnergyModifierKind.FreeThisTurn or
            LocalEnergyModifierKind.FreeUntilPlayed or
            LocalEnergyModifierKind.FreeThisCombat or
            LocalEnergyModifierKind.SetThisTurn or
            LocalEnergyModifierKind.SetThisTurnOrUntilPlayed or
            LocalEnergyModifierKind.SetThisCombat;

        public bool ClearsAfterPlay => Kind is
            LocalEnergyModifierKind.FreeUntilPlayed or
            LocalEnergyModifierKind.SetThisTurnOrUntilPlayed or
            LocalEnergyModifierKind.AddUntilPlayed;

    }

    public virtual int EnergyCost => ResolveEnergyCost(out _);

    /// <summary>Energy cost after ordered card-local changes, before combat-wide cost hooks.</summary>
    public int LocalEnergyCost => ResolveLocalEnergyCost();

    private int ResolveEnergyCost(out bool hookModified)
    {
        hookModified = false;
        int resolvedCost = ResolveLocalEnergyCost();
        if (IsXEnergyCost)
            return resolvedCost;

        if (CombatState is not ICombatState combatState)
        {
            return resolvedCost;
        }

        decimal modified = Hook.ModifyEnergyCostInCombat(
            combatState, this, resolvedCost, out hookModified);
        return hookModified ? (int)Math.Max(0m, modified) : resolvedCost;
    }

    private int ResolveLocalEnergyCost()
    {
        if (CanonicalEnergyCost < 0)
            return CanonicalEnergyCost;
        // Native CardEnergyCost.GetWithModifiers returns the base cost for CostsX.
        if (IsXEnergyCost)
            return CanonicalEnergyCost;
        int cost = Math.Max(0, CanonicalEnergyCost - _energyCostUpgradeDelta);
        foreach (LocalEnergyModifier modifier in _localEnergyModifiers)
        {
            cost = modifier.IsAbsolute ? modifier.Amount : cost + modifier.Amount;
        }
        return Math.Max(0, cost);
    }

    public virtual int StarCost => ResolveStarCost(out _);

    /// <summary>Card-local star cost before combat-wide hooks are applied.</summary>
    public int LocalStarCost => TemporaryStarCostOverrideThisTurn ??
        (TemporaryFreeThisTurn || TemporaryFreeUntilPlayed || TemporaryFreeThisCombat
            ? 0 : CanonicalStarCost);

    private int ResolveStarCost(out bool hookModified)
    {
        hookModified = false;
        int resolvedCost = LocalStarCost;
        if (CombatState is not ICombatState combatState)
        {
            return resolvedCost;
        }

        decimal modified = Hook.ModifyStarCostInCombat(
            combatState, this, resolvedCost, out hookModified);
        return hookModified ? (int)Math.Max(0m, modified) : resolvedCost;
    }

    public bool HasStarCost => CanonicalStarCost >= 0 || IsXStarCost;

    /// <summary>供 Jackpot 等"只生成0费卡"效果判断——真实游戏读 <c>CardEnergyCost.CostsX</c>，
    /// 本项目没有那层修饰符对象，直接暴露 <see cref="IsXEnergyCost"/>。</summary>
    public bool CostsXEnergy => IsXEnergyCost;

    public bool CostsXStar => IsXStarCost;

    /// <summary>A transient copy created for effects that replay a card without retaining it.</summary>
    public bool IsDupe { get; private set; }

    private bool _exhaustOnNextPlay;

    /// <summary>For draw-pile auto-play effects such as Havoc. The command sets this before
    /// attempting the play; normal result-pile resolution consumes it.</summary>
    public bool ExhaustOnNextPlay
    {
        get => _exhaustOnNextPlay;
        internal set { AssertMutable(); _exhaustOnNextPlay = value; }
    }

    public int CurrentUpgradeLevel { get; private set; }

    /// <summary>原版 <c>CardModel.CompareTo</c>：先按 <c>Id</c>，相同再按 <see cref="CurrentUpgradeLevel"/>（普通牌在前）。
    /// 洗牌前的 <c>StableShuffle</c> 依赖这个顺序；只按 <c>Id</c> 比较会让同名的普通牌与升级牌在不稳定的
    /// <c>List.Sort</c> 里互换位置，同样的随机数洗出不同的抽牌堆。</summary>
    public override int CompareTo(AbstractModel? other)
    {
        if (ReferenceEquals(this, other))
        {
            return 0;
        }

        if (other is null)
        {
            return 1;
        }

        int byId = base.CompareTo(other);
        if (byId != 0)
        {
            return byId;
        }

        return CurrentUpgradeLevel.CompareTo(((CardModel)other).CurrentUpgradeLevel);
    }

    public virtual int MaxUpgradeLevel => 1;

    public bool IsUpgraded => CurrentUpgradeLevel > 0;

    public bool IsUpgradable => CurrentUpgradeLevel < MaxUpgradeLevel;

    /// <summary>偏离 #105（部分解决 #32）：额外重复出牌次数,逐字移植 <c>CardModel.BaseReplayCount</c>；
    /// 偏离 #146 的附魔窄切片先折叠 <c>Enchantment.EnchantPlayCount</c>，再交给全局
    /// <c>Hook.ModifyCardPlayCount</c>；
    /// <see cref="PlayAsync"/> 直接把这个值当最终重复次数用。</summary>
    public int BaseReplayCount { get; set; }

    /// <summary>本回合剩余时间内把能量费用覆写为0；星愿费用不受影响。</summary>
    public bool TemporaryFreeThisTurn { get; private set; }

    /// <summary>Retain this card only through the current player turn's hand flush.</summary>
    public bool TemporaryRetainThisTurn { get; private set; }

    /// <summary>Sly applies only through the current player's turn.</summary>
    public bool TemporarySlyThisTurn { get; private set; }

    internal void ApplyTemporaryRetainThisTurn()
    {
        AssertMutable();
        TemporaryRetainThisTurn = true;
    }

    internal void ClearTemporaryRetainThisTurn()
    {
        AssertMutable();
        TemporaryRetainThisTurn = false;
    }

    internal void ApplyTemporarySlyThisTurn()
    {
        AssertMutable();
        TemporarySlyThisTurn = true;
    }

    internal void ClearTemporarySlyThisTurn()
    {
        AssertMutable();
        TemporarySlyThisTurn = false;
    }

    public void MakeTemporaryFreeThisTurn()
    {
        AssertMutable();
        TemporaryFreeThisTurn = true;
        _localEnergyModifiers.Add(new(LocalEnergyModifierKind.FreeThisTurn, 0));
    }

    internal void ClearTemporaryFreeThisTurn()
    {
        AssertMutable();
        TemporaryFreeThisTurn = false;
        _localEnergyModifiers.RemoveAll(modifier => modifier.Kind == LocalEnergyModifierKind.FreeThisTurn);
    }

    /// <summary>Remains free across turn boundaries until the card is actually played.</summary>
    // 偏离 #139：依照 Plan06d 的 MakeFreeUntilPlayed 设计，首次打出后清除；真实 SetToFreeThisCombat 会持续整场战斗。
    public bool TemporaryFreeUntilPlayed { get; private set; }

    public void MakeFreeUntilPlayed()
    {
        AssertMutable();
        TemporaryFreeUntilPlayed = true;
        _localEnergyModifiers.Add(new(LocalEnergyModifierKind.FreeUntilPlayed, 0));
    }

    /// <summary>Remains free for both energy and star costs through this combat.</summary>
    public bool TemporaryFreeThisCombat { get; private set; }

    public void MakeFreeThisCombat()
    {
        AssertMutable();
        TemporaryFreeThisCombat = true;
        _localEnergyModifiers.Add(new(LocalEnergyModifierKind.FreeThisCombat, 0));
    }

    /// <summary>A fixed energy-cost override for the current turn, with precedence over free flags.</summary>
    public int? TemporaryCostOverrideThisTurn { get; private set; }

    public void SetTemporaryCostOverrideThisTurn(int cost)
    {
        AssertMutable();
        ArgumentOutOfRangeException.ThrowIfNegative(cost);
        TemporaryCostOverrideThisTurn = cost;
        _localEnergyModifiers.Add(new(LocalEnergyModifierKind.SetThisTurn, cost));
    }

    internal void ClearTemporaryCostOverrideThisTurn()
    {
        AssertMutable();
        TemporaryCostOverrideThisTurn = null;
        _localEnergyModifiers.RemoveAll(modifier => modifier.Kind == LocalEnergyModifierKind.SetThisTurn);
    }

    /// <summary>A fixed energy-cost override until the turn ends or this card is first played.</summary>
    public int? TemporaryCostOverrideThisTurnOrUntilPlayed { get; private set; }

    /// <summary>The independent star-cost part of SetToFreeThisTurn; unlike energy it survives playing.</summary>
    public int? TemporaryStarCostOverrideThisTurn { get; private set; }

    public void SetToFreeThisTurn()
    {
        AssertMutable();
        // Upstream local cost modifiers do not replace either kind of X cost.
        if (!CostsXEnergy)
        {
            SetTemporaryCostOverrideThisTurnOrUntilPlayed(0);
        }
        if (!CostsXStar)
        {
            TemporaryStarCostOverrideThisTurn = 0;
        }
    }

    internal void ClearTemporaryStarCostOverrideThisTurn()
    {
        AssertMutable();
        TemporaryStarCostOverrideThisTurn = null;
    }

    public void SetTemporaryCostOverrideThisTurnOrUntilPlayed(int cost)
    {
        AssertMutable();
        ArgumentOutOfRangeException.ThrowIfNegative(cost);
        TemporaryCostOverrideThisTurnOrUntilPlayed = cost;
        _localEnergyModifiers.Add(new(LocalEnergyModifierKind.SetThisTurnOrUntilPlayed, cost));
    }

    internal void ClearTemporaryCostOverrideThisTurnOrUntilPlayed()
    {
        AssertMutable();
        TemporaryCostOverrideThisTurnOrUntilPlayed = null;
        _localEnergyModifiers.RemoveAll(modifier => modifier.Kind == LocalEnergyModifierKind.SetThisTurnOrUntilPlayed);
    }

    private CardModel? _deckVersion;
    private CardModel? _cloneOf;

    /// <summary>The source of a combat-created clone; distinct from persistent-deck identity.</summary>
    public CardModel? CloneOf => _cloneOf;

    /// <summary>The persistent deck card represented by this combat copy, if any.</summary>
    public CardModel? DeckVersion => _deckVersion;

    internal void AssignDeckVersionInternal(CardModel? deckVersion)
    {
        AssertMutable();
        _deckVersion = deckVersion;
    }

    public int? TemporaryCostOverrideThisCombat { get; private set; }

    public void SetTemporaryCostOverrideThisCombat(int cost)
    {
        AssertMutable();
        ArgumentOutOfRangeException.ThrowIfNegative(cost);
        TemporaryCostOverrideThisCombat = cost;
        _localEnergyModifiers.Add(new(LocalEnergyModifierKind.SetThisCombat, cost));
    }

    /// <summary>Adds a local modifier after the currently resolved local cost for this combat.</summary>
    public void AddEnergyCostThisCombat(int amount)
    {
        AssertMutable();
        _energyCostThisCombatDelta = checked(_energyCostThisCombatDelta + amount);
        if (amount != 0)
            _localEnergyModifiers.Add(new(LocalEnergyModifierKind.AddThisCombat, amount));
    }

    /// <summary>Adds a relative local energy modifier lasting until the turn ends (including replays).</summary>
    public void AddEnergyCostThisTurn(int amount)
    {
        AssertMutable();
        _energyCostThisTurnDelta = checked(_energyCostThisTurnDelta + amount);
        if (amount != 0)
            _localEnergyModifiers.Add(new(LocalEnergyModifierKind.AddThisTurn, amount));
    }

    /// <summary>Adds a relative local cost modifier that survives turns until the first play.</summary>
    public void AddEnergyCostUntilPlayed(int amount)
    {
        AssertMutable();
        if (amount != 0)
            _localEnergyModifiers.Add(new(LocalEnergyModifierKind.AddUntilPlayed, amount));
    }

    internal void ClearEnergyCostThisTurn()
    {
        AssertMutable();
        _energyCostThisTurnDelta = 0;
        _localEnergyModifiers.RemoveAll(modifier => modifier.Kind == LocalEnergyModifierKind.AddThisTurn);
    }

    /// <summary>
    /// The source enum also contains Sly; Eternal is modeled for persistent-deck cards.
    /// </summary>
    protected virtual IReadOnlyCollection<CardKeyword> CanonicalKeywords => Array.Empty<CardKeyword>();

    protected virtual IReadOnlyCollection<CardTag> CanonicalTags => Array.Empty<CardTag>();

    private HashSet<CardKeyword>? _keywordOverride;

    private List<EnchantmentModel> _enchantments = new();

    private AfflictionModel? _affliction;

    public IReadOnlyCollection<CardKeyword> Keywords
    {
        get
        {
            IReadOnlyCollection<CardKeyword> keywords =
                (IReadOnlyCollection<CardKeyword>?)_keywordOverride ?? CanonicalKeywords;
            if (_affliction is null)
            {
                return keywords;
            }

            var modifiedKeywords = new HashSet<CardKeyword>(keywords);
            return _affliction.TryModifyKeywords(modifiedKeywords) ? modifiedKeywords : keywords;
        }
    }

    public bool IsRemovable => Pile?.Type != PileType.Deck || !HasKeyword(CardKeyword.Eternal);

    public bool IsTransformable => Pile?.Type != PileType.Deck || !HasKeyword(CardKeyword.Eternal);

    public IReadOnlyCollection<CardTag> Tags => CanonicalTags;

    public IReadOnlyList<EnchantmentModel> Enchantments => _enchantments;

    public AfflictionModel? Affliction => _affliction;

    public Player Owner { get; private set; } = null!;

    internal bool HasOwner => Owner is not null;

    public CardPile? Pile { get; private set; }


    private int? _floorAddedToDeck;

    /// <summary>The run floor on which this card permanently entered its owner's deck, or null if unknown.</summary>
    public int? FloorAddedToDeck
    {
        get => _floorAddedToDeck;
        set
        {
            AssertMutable();
            _floorAddedToDeck = value;
        }
    }

    public ICombatState? CombatState => Owner?.Creature.CombatState;

    public override bool ShouldReceiveCombatHooks => true;

    /// <summary>Whether this card has an effect while retained in hand at turn end. Mirrors the real game's CardModel.HasTurnEndInHandEffect.</summary>
    protected virtual bool HasTurnEndInHandEffect => false;

    /// <summary>The turn-end effect of a card held in hand. 原版 <c>CombatManager.DoTurnEndCards</c> 先把牌移入打出区再调用，
    /// 所以结算时这张牌已不在手里；需要"结算前的手牌"的卡（Regret）在 <c>BeforeSideTurnEnd</c> 里自行记录。</summary>
    protected virtual Task OnTurnEndInHand() => Task.CompletedTask;

    protected bool IsEligibleForTurnEndInHandEffect(
        CombatSide side,
        IEnumerable<Creature> participants) =>
        HasTurnEndInHandEffect &&
        side == CombatSide.Player &&
        Pile?.Type == PileType.Hand &&
        Owner is not null &&
        participants.Contains(Owner.Creature);

    internal bool HasTurnEndInHandEffectInternal => HasTurnEndInHandEffect;

    /// <summary>Read-only turn-end hand-effect flag for branch evaluation.</summary>
    public bool HasTurnEndInHandEffectForPrediction => HasTurnEndInHandEffect;

    /// <summary>由 <see cref="Combat.CombatEngine"/> 的回合结束流程在把牌移入打出区之后调用（原版 <c>OnTurnEndInHandWrapper</c>）。</summary>
    internal Task ResolveTurnEndInHandEffect() => OnTurnEndInHand();

    public bool HasKeyword(CardKeyword keyword) =>
        (keyword == CardKeyword.Retain && TemporaryRetainThisTurn) ||
        (keyword == CardKeyword.Sly && TemporarySlyThisTurn) ||
        Keywords.Contains(keyword);

    public void Upgrade()
    {
        AssertMutable();
        if (!IsUpgradable)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} is not upgradable (level {CurrentUpgradeLevel}/{MaxUpgradeLevel}).");
        }

        CurrentUpgradeLevel++;
        OnUpgrade();
    }

    protected virtual void OnUpgrade()
    {
    }

    public void Downgrade()
    {
        AssertMutable();
        if (CurrentUpgradeLevel <= 0)
        {
            throw new InvalidOperationException("Card is not upgraded.");
        }

        CardModel lowerLevel = CreateCanonicalReplay(CurrentUpgradeLevel - 1);
        CardModel currentLevel = CreateCanonicalReplay(CurrentUpgradeLevel);
        Dictionary<FieldInfo, object> lowerNumeric = lowerLevel.CaptureUpgradeNumericState();
        Dictionary<FieldInfo, object> currentNumeric = currentLevel.CaptureUpgradeNumericState();

        var restoredKeywords = new HashSet<CardKeyword>(Keywords);
        restoredKeywords.ExceptWith(currentLevel.Keywords.Except(lowerLevel.Keywords));
        restoredKeywords.UnionWith(lowerLevel.Keywords.Except(currentLevel.Keywords));
        foreach ((FieldInfo field, object currentCanonicalValue) in currentNumeric)
        {
            object lowerCanonicalValue = lowerNumeric[field];
            if (Equals(currentCanonicalValue, lowerCanonicalValue))
            {
                continue;
            }

            object runtimeValue = field.GetValue(this)
                ?? throw new InvalidOperationException(
                    $"Upgrade field {field.Name} unexpectedly became null.");
            object upgradeDelta = SubtractNumeric(
                currentCanonicalValue, lowerCanonicalValue, field.FieldType);
            field.SetValue(
                this,
                SubtractNumeric(runtimeValue, upgradeDelta, field.FieldType));
        }

        CurrentUpgradeLevel--;
        SetEffectiveKeywords(restoredKeywords);
    }

    private Dictionary<FieldInfo, object> CaptureUpgradeNumericState()
    {
        var result = new Dictionary<FieldInfo, object>();
        for (Type? type = GetType();
             type is not null && typeof(CardModel).IsAssignableFrom(type);
             type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.Public |
                         BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                Type fieldType = field.FieldType;
                if (field.Name == "<CurrentUpgradeLevel>k__BackingField")
                {
                    continue;
                }

                if (!field.IsStatic &&
                    (fieldType == typeof(int) || fieldType == typeof(decimal)))
                {
                    result.Add(
                        field,
                        field.GetValue(this)
                            ?? throw new InvalidOperationException(
                                $"Upgrade field {field.Name} unexpectedly became null."));
                }
            }
        }

        return result;
    }

    private CardModel CreateCanonicalReplay(int upgradeLevel)
    {
        var replay = (CardModel)ModelDb.Get(GetType()).MutableClone();
        for (int level = 0; level < upgradeLevel; level++)
        {
            replay.Upgrade();
        }

        return replay;
    }

    private void SetEffectiveKeywords(IEnumerable<CardKeyword> keywords)
    {
        var restored = new HashSet<CardKeyword>(keywords);
        _keywordOverride = restored.SetEquals(CanonicalKeywords) ? null : restored;
    }

    private static object SubtractNumeric(object left, object right, Type fieldType)
    {
        if (fieldType == typeof(int))
        {
            return checked((int)left - (int)right);
        }

        if (fieldType == typeof(decimal))
        {
            return (decimal)left - (decimal)right;
        }

        throw new InvalidOperationException(
            $"Unsupported numeric upgrade field type {fieldType.FullName}.");
    }

    protected void ReduceEnergyCost(int amount)
    {
        AssertMutable();
        _energyCostUpgradeDelta += amount;
    }

    protected void AddKeyword(CardKeyword keyword)
    {
        AssertMutable();
        _keywordOverride = new HashSet<CardKeyword>(Keywords) { keyword };
    }

    internal void AddKeywordInternal(CardKeyword keyword) => AddKeyword(keyword);

    protected void RemoveKeyword(CardKeyword keyword)
    {
        AssertMutable();
        var updated = new HashSet<CardKeyword>(Keywords);
        updated.Remove(keyword);
        _keywordOverride = updated;
    }

    internal void RemoveKeywordInternal(CardKeyword keyword) => RemoveKeyword(keyword);

    public void AssignOwner(Player owner)
    {
        AssertMutable();
        Owner = owner;
    }

    internal void AssignOwnerInternal(Player? owner)
    {
        AssertMutable();
        Owner = owner!;
    }

    public void GiveToAnotherPlayer(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        AssertMutable();
        Owner = player;
    }

    internal void AssignPileInternal(CardPile? pile)
    {
        AssertMutable();
        Pile = pile;
    }

    internal void AttachEnchantment(EnchantmentModel enchantment)
    {
        ArgumentNullException.ThrowIfNull(enchantment);
        AssertMutable();
        if (_enchantments.Count > 0)
        {
            throw new InvalidOperationException($"{Id} is already enchanted.");
        }

        enchantment.AssignOwnerCard(this);
        _enchantments.Add(enchantment);
    }

    internal void AttachAffliction(AfflictionModel affliction, decimal amount)
    {
        ArgumentNullException.ThrowIfNull(affliction);
        AssertMutable();
        if (_affliction is not null)
        {
            throw new InvalidOperationException($"{Id} is already afflicted.");
        }

        affliction.Attach(this, amount);
        _affliction = affliction;
    }

    internal void ClearAfflictionInternal()
    {
        AssertMutable();
        _affliction?.Clear();
        _affliction = null;
    }

    /// <summary>Manual play applies both global prevention and the card's own playability rule.</summary>
    public bool CanPlay(out UnplayableReason reason)
    {
        reason = UnplayableReason.None;
        if (HasKeyword(CardKeyword.Unplayable))
        {
            reason |= UnplayableReason.HasUnplayableKeyword;
        }

        if (CombatState is { } combatState && !Hook.ShouldPlay(combatState, this, isAutoPlay: false))
        {
            reason |= UnplayableReason.BlockedByHook;
        }

        if (TargetType == TargetType.AnyAlly &&
            CombatState is { } allyState &&
            CombatTargetCandidates.ForCard(allyState, Owner, TargetType).Count == 0)
        {
            reason |= UnplayableReason.NoLivingAllies;
        }

        if (Owner.PlayerCombatState!.Energy < EnergyCost)
        {
            reason |= UnplayableReason.EnergyCostTooHigh;
        }

        if (Owner.PlayerCombatState!.Stars < StarCost)
        {
            reason |= UnplayableReason.StarCostTooHigh;
        }

        if (!IsPlayable)
        {
            reason |= UnplayableReason.BlockedByCardLogic;
        }

        return reason == UnplayableReason.None;
    }

    private (int energySpent, int starsSpent) SpendResources(
        int resolvedEnergyCost,
        int resolvedStarCost,
        bool starHookModified)
    {
        AssertMutable();
        PlayerCombatState combatState = Owner.PlayerCombatState!;
        int energyToSpend = IsXEnergyCost
            ? combatState.Energy
            : Math.Max(0, Math.Min(resolvedEnergyCost, combatState.Energy));
        if (energyToSpend > 0 && CombatState is CombatState concreteState &&
            concreteState.IsLiveCombat())
            concreteState.SemanticHistory.RecordEnergySpent(concreteState, Owner, energyToSpend);
        combatState.LoseEnergy(energyToSpend);
        bool hasTemporaryStarFree = TemporaryStarCostOverrideThisTurn.HasValue ||
            TemporaryFreeThisTurn || TemporaryFreeUntilPlayed || TemporaryFreeThisCombat;
        int starsToSpend = IsXStarCost && !hasTemporaryStarFree && !starHookModified
            ? combatState.Stars
            : Math.Max(0, Math.Min(resolvedStarCost, combatState.Stars));
        combatState.LoseStars(starsToSpend);
        return (energyToSpend, starsToSpend);
    }

    /// <summary>决定出牌后卡牌去哪个牌堆。逐字移植默认实现（Power 卡直接离开战斗,其余进弃牌堆）。</summary>
    protected virtual CardLocation GetResultLocationForCardPlay()
    {
        if (IsDupe)
        {
            return new CardLocation(Owner, PileType.None, CardPilePosition.Bottom);
        }

        if (Type == CardType.Power)
        {
            return new CardLocation(Owner, PileType.None, CardPilePosition.Bottom);
        }

        if (ExhaustOnNextPlay || HasKeyword(CardKeyword.Exhaust))
        {
            ExhaustOnNextPlay = false;
            return new CardLocation(Owner, PileType.Exhaust, CardPilePosition.Bottom);
        }

        return new CardLocation(Owner, PileType.Discard, CardPilePosition.Bottom);
    }

    protected virtual Task OnPlay(CardPlay cardPlay) => Task.CompletedTask;

    /// <summary>
    /// 出牌总控,对应游戏 <c>CardModel.OnPlayWrapper</c> 的精简版。不在内部自检 <see cref="CanPlay"/>——
    /// 真实游戏的 OnPlayWrapper 同样不做这个检查,可玩性门禁是调用方（UI 或 CombatEngine.PlayCardAsync）的职责,
    /// 这样才能保留"强制出牌"路径（如未来的自动出牌 power）：能量不足时仍会执行,只是 SpendResources 会把花费
    /// 钳制到当前可用能量,不会扣成负数。
    /// </summary>
    public Task PlayAsync(Creature? target) => PlayWithResultAsync(target);

    /// <summary>Plays the card and returns the first resolved play for reporting consumers.</summary>
    public Task<CardPlay?> PlayWithResultAsync(Creature? target) =>
        PlayInternalAsync(target, isAutoPlay: false);

    internal Task AutoPlayAsync(Creature? target) => AutoPlayWithResultAsync(target);

    internal Task<CardPlay?> AutoPlayWithResultAsync(Creature? target) =>
        PlayInternalAsync(target, isAutoPlay: true);
    internal Task<CardPlay?> AutoPlayPayingCostsWithResultAsync(Creature? target) =>
        PlayInternalAsync(target, isAutoPlay: true, spendResources: true);
    internal Task<CardPlay?> AutoPlayPrevalidatedWithResultAsync(Creature? target) =>
        PlayInternalAsync(target, isAutoPlay: true, shouldPlayPrevalidated: true);


    internal async Task MoveToResultPileWithoutPlaying(ICombatState combatState)
    {
        if (IsDupe)
        {
            CardPileCmd.Remove(this);
            return;
        }

        if (ExhaustOnNextPlay || HasKeyword(CardKeyword.Exhaust))
        {
            await CardPileCmd.Exhaust(combatState, this);
            return;
        }

        Player originalOwner = Owner;
        bool departedHand = Pile?.Type == PileType.Hand;
        CardPileCmd.Add(this, PileType.Discard);
        await CardPileCmd.NotifyHandDeparture(combatState, originalOwner, departedHand);
    }

    private async Task<CardPlay?> PlayInternalAsync(
        Creature? target,
        bool isAutoPlay,
        bool shouldPlayPrevalidated = false,
        bool spendResources = false)
    {
        AssertMutable();
        ICombatState combatState = CombatState ?? throw new InvalidOperationException("Card has no combat state.");
        if (!shouldPlayPrevalidated && !Hook.ShouldPlay(combatState, this, isAutoPlay))
        {
            if (isAutoPlay)
            {
                await MoveToResultPileWithoutPlaying(combatState);
            }

            return null;
        }

        ICombatObserver? observer = (combatState as CombatState)?.Observer;
        using IDisposable? rngScope = (combatState as CombatState)?.BeginCardRngScope(this);
        try
        {
            observer?.CardPlayStarted(this, target);
            return await PlayAfterObserverStartedAsync(
                combatState,
                target,
                isAutoPlay,
                spendResources,
                observer);
        }
        catch
        {
            (combatState as CombatState)?.Engine?.CancelRequestedEndPlayerTurn();
            TryAbortCardPlay(observer, this, target);
            throw;
        }
    }

    private async Task<CardPlay?> PlayAfterObserverStartedAsync(
        ICombatState combatState,
        Creature? target,
        bool isAutoPlay,
        bool spendResources,
        ICombatObserver? observer)
    {
        Player originalOwner = Owner;

        PlayerCombatState playerCombatState = Owner.PlayerCombatState!;
        int resolvedEnergyCost = ResolveEnergyCost(out _);
        int resolvedStarCost = ResolveStarCost(out bool starHookModified);
        bool hasTemporaryStarCost = TemporaryStarCostOverrideThisTurn.HasValue ||
            TemporaryFreeThisTurn || TemporaryFreeUntilPlayed || TemporaryFreeThisCombat;
        int energyValue = IsXEnergyCost
            ? playerCombatState.Energy
            : Math.Max(0, resolvedEnergyCost);
        int starValue = IsXStarCost && !hasTemporaryStarCost && !starHookModified
            ? playerCombatState.Stars
            : Math.Max(0, resolvedStarCost);
        CardPileCmd.Add(this, PileType.Play);
        (int energySpent, int starsSpent) = isAutoPlay && !spendResources
            ? (0, 0)
            : SpendResources(
                resolvedEnergyCost,
                resolvedStarCost,
                starHookModified);
        if (energySpent > 0)
        {
            await Hook.AfterEnergySpent(combatState, this, energySpent);
        }
        if (starsSpent > 0)
        {
            await Hook.AfterStarsSpent(combatState, starsSpent, Owner);
        }
        var resources = new ResourceInfo(energySpent, energyValue, starsSpent, starValue);
        CardLocation resultLocation = Hook.ModifyCardPlayResultLocation(
            combatState,
            this,
            isAutoPlay,
            resources,
            GetResultLocationForCardPlay(),
            out IReadOnlyList<AbstractModel> resultLocationModifiers);
        await Hook.AfterModifyingCardPlayResultLocation(this, resultLocation, resultLocationModifiers);

        int enchantedPlayCount = BaseReplayCount + 1;
        foreach (EnchantmentModel enchantment in _enchantments)
        {
            enchantedPlayCount = enchantment.EnchantPlayCount(enchantedPlayCount);
        }

        int playCount = Hook.ModifyCardPlayCount(
            combatState,
            this,
            enchantedPlayCount,
            target,
            out List<AbstractModel> playCountModifiers);
        await Hook.AfterModifyingCardPlayCount(combatState, this, playCountModifiers);
        CardPlay? firstPlay = null;
        playerCombatState.CardOrPotionEffectDepth++;
        try
        {
            for (int playIndex = 0; playIndex < playCount; playIndex++)
            {
                var cardPlay = new CardPlay
                {
                    Card = this,
                    Player = Owner,
                    Target = target,
                    ResultPile = resultLocation.PileType,
                    Resources = resources,
                    IsAutoPlay = isAutoPlay,
                    PlayIndex = playIndex,
                    PlayCount = playCount,
                    PlayOrdinal = playerCombatState.CardPlaysStartedThisTurn + 1,
                };
                firstPlay ??= cardPlay;
                await Hook.BeforeCardPlayed(combatState, cardPlay);
                playerCombatState.RecordCardPlayStarted(cardPlay);
                await OnPlay(cardPlay);
                foreach (EnchantmentModel enchantment in _enchantments.ToArray())
                {
                    await enchantment.OnPlay(this, cardPlay);
                    enchantment.InvokeExecutionFinished();
                }
                if (_affliction is not null)
                {
                    await _affliction.OnPlay(cardPlay);
                    _affliction.InvokeExecutionFinished();
                }
                playerCombatState.RecordCardPlayed(this, isAutoPlay);
                await Hook.AfterCardPlayed(combatState, cardPlay);
                if (Owner.Creature.IsDead)
                {
                    break;
                }
            }
        }
        finally
        {
            playerCombatState.CardOrPotionEffectDepth--;
        }

        Player? ownerBeforeTransfer = null;
        if (resultLocation.PileType != PileType.None && !ReferenceEquals(resultLocation.Player, Owner))
        {
            ownerBeforeTransfer = Owner;
            GiveToAnotherPlayer(resultLocation.Player);
        }

        if (Pile?.Type == PileType.Play)
        {
            if (resultLocation.PileType == PileType.None)
            {
                CardPileCmd.Remove(this);
            }
            else if (resultLocation.PileType == PileType.Exhaust)
            {
                await CardPileCmd.Exhaust(combatState, this);
            }
            else
            {
                CardPileCmd.Add(this, resultLocation.PileType, resultLocation.Position);
            }
        }

        if (ownerBeforeTransfer is not null)
        {
            await Hook.AfterCardOwnerChanged(combatState, this, ownerBeforeTransfer);
        }

        await CardPileCmd.CheckForEmptyHand(combatState, originalOwner);
        if (TemporaryFreeUntilPlayed)
        {
            TemporaryFreeUntilPlayed = false;
        }
        TemporaryCostOverrideThisTurnOrUntilPlayed = null;
        _localEnergyModifiers.RemoveAll(modifier => modifier.ClearsAfterPlay);

        observer?.CardPlayFinished(this, target, firstPlay);
        return firstPlay;
    }

    private static void TryAbortCardPlay(
        ICombatObserver? observer,
        CardModel card,
        Creature? target)
    {
        try
        {
            observer?.CardPlayAborted(card, target);
        }
        catch
        {
            // Preserve the original core or observer callback exception.
        }
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _localEnergyModifiers = _localEnergyModifiers.ToList();
        _keywordOverride = _keywordOverride is null ? null : new HashSet<CardKeyword>(_keywordOverride);
        _enchantments = _enchantments
            .Select(enchantment => (EnchantmentModel)enchantment.MutableClone())
            .ToList();
        foreach (EnchantmentModel enchantment in _enchantments)
        {
            enchantment.AssignOwnerCard(this);
        }
        if (_affliction is not null)
        {
            int amount = _affliction.Amount;
            AfflictionModel clone = (AfflictionModel)_affliction.MutableClone();
            _affliction = null;
            AttachAffliction(clone, amount);
        }
    }

    protected override void AfterCloned()
    {
        base.AfterCloned();
        Pile = null;
        TemporarySlyThisTurn = false;
        TemporaryFreeThisTurn = false;
        TemporaryRetainThisTurn = false;
        TemporaryFreeUntilPlayed = false;
        TemporaryFreeThisCombat = false;
        TemporaryCostOverrideThisTurn = null;
        TemporaryCostOverrideThisTurnOrUntilPlayed = null;
        TemporaryCostOverrideThisCombat = null;
        TemporaryStarCostOverrideThisTurn = null;
        ExhaustOnNextPlay = false;
        _energyCostThisTurnDelta = 0;
        _localEnergyModifiers.RemoveAll(modifier =>
            modifier.Kind != LocalEnergyModifierKind.AddThisCombat);
        _deckVersion = null;
        _cloneOf = null;
    }

    internal virtual void RestoreCombatCloneReferencesFrom(
        CardModel source,
        IReadOnlyDictionary<CardModel, CardModel> cardMap)
    {
        _deckVersion = source._deckVersion is not null && cardMap.TryGetValue(source._deckVersion, out CardModel? mapped)
            ? mapped
            : source._deckVersion;
        _cloneOf = source._cloneOf is not null ? cardMap[source._cloneOf] : null;
    }

    internal CardModel CloneForCombat(Player? owner)
    {
        var clone = (CardModel)MutableClone();
        clone.AssignOwnerInternal(owner);
        clone._deckVersion = _deckVersion;
        clone._cloneOf = _cloneOf;
        foreach ((EnchantmentModel source, EnchantmentModel target) in _enchantments.Zip(clone._enchantments))
        {
            target.RestoreCombatCloneStateFrom(source);
        }

        clone.TemporaryFreeThisTurn = TemporaryFreeThisTurn;
        clone.TemporaryRetainThisTurn = TemporaryRetainThisTurn;
        clone.TemporarySlyThisTurn = TemporarySlyThisTurn;
        clone.TemporaryFreeUntilPlayed = TemporaryFreeUntilPlayed;
        clone.TemporaryFreeThisCombat = TemporaryFreeThisCombat;
        clone.TemporaryCostOverrideThisTurn = TemporaryCostOverrideThisTurn;
        clone.TemporaryCostOverrideThisTurnOrUntilPlayed = TemporaryCostOverrideThisTurnOrUntilPlayed;
        clone.TemporaryCostOverrideThisCombat = TemporaryCostOverrideThisCombat;
        clone.TemporaryStarCostOverrideThisTurn = TemporaryStarCostOverrideThisTurn;
        clone.ExhaustOnNextPlay = ExhaustOnNextPlay;
        clone._energyCostThisTurnDelta = _energyCostThisTurnDelta;
        clone._localEnergyModifiers = _localEnergyModifiers.ToList();
        return clone;
    }

    /// <summary>Creates a generated combat card while preserving its mutable costs and recording its source.</summary>
    public CardModel CreateClone()
    {
        AssertMutable();
        if (Pile?.Type == PileType.Deck)
        {
            throw new InvalidOperationException("Cannot create a combat clone of a persistent deck card.");
        }
        CardModel clone = CloneForCombat(Owner);
        clone.ExhaustOnNextPlay = false;
        clone._deckVersion = null;
        clone._cloneOf = this;
        return clone;
    }

    /// <summary>
    /// Creates a combat-only replay copy. Dupes preserve the source's current combat state, but have no
    /// persistent-deck identity and always leave combat instead of entering a result pile.
    /// </summary>
    internal CardModel CreateDupe(Player owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var dupe = CloneForCombat(owner);
        dupe.IsDupe = true;
        dupe._deckVersion = null;
        dupe._cloneOf = IsDupe ? _cloneOf ?? this : this;
        dupe._floorAddedToDeck = null;
        dupe.RemoveKeywordInternal(CardKeyword.Exhaust);
        return dupe;
    }

    internal virtual void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
    }

    internal void AppendIntrinsicCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context)
    {
        // Called by central search dispatch, outside the overridable/reimplemented contributor.
        // Resolved costs alone hide these deltas behind temporary overrides.
        builder.Append(_energyCostUpgradeDelta);
        builder.Append(_energyCostThisCombatDelta);
        builder.Append(_energyCostThisTurnDelta);
        builder.Append(_localEnergyModifiers.Count);
        foreach (LocalEnergyModifier modifier in _localEnergyModifiers)
        {
            builder.Append((int)modifier.Kind);
            builder.Append(modifier.Amount);
        }
        builder.Append(TemporaryStarCostOverrideThisTurn ?? int.MinValue);
        builder.Append(IsDupe);
        builder.Append(ExhaustOnNextPlay);
        builder.Append(_cloneOf is not null);
        if (_cloneOf is not null)
        {
            context.AppendCardCloneOrigin(ref builder, _cloneOf);
        }
    }

    void ICombatStateDescriptionContributor.AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        AppendCombatStateDescription(ref builder, context);
}
