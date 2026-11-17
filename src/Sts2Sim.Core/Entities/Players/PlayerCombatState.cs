using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Orbs;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Players;

/// <summary>玩家的战斗期状态：能量/星愿/回合阶段/牌堆/宠物/充能球。
/// 偏离：Stars 无回合重置逻辑（对照真实源码核实，只由 GainStars/LoseStars 显式增减）。</summary>
public sealed class PlayerCombatState
{
    private readonly Player _player;
    private readonly List<ArrowSlot> _arrows = new();

    public PlayerCombatState(Player player)
    {
        _player = player;
        OrbQueue = NewOrbQueue(player);
        for (int i = 0; i < player.Character.BaseArrowSlotCount; i++)
        {
            GainArrow();
        }
    }

    private static OrbQueue NewOrbQueue(Player owner)
    {
        var queue = new OrbQueue(owner);
        queue.Clear();
        queue.AddCapacity(owner.BaseOrbSlotCount);
        return queue;
    }

    public OrbQueue OrbQueue { get; private set; }

    public IReadOnlyList<ArrowSlot> Arrows => _arrows;

    public int AvailableArrowCount => _arrows.Count(arrow => arrow.IsAvailable);

    public void GainArrow() => _arrows.Add(new ArrowSlot(_player));

    internal void ReplaceClonedArrows(IEnumerable<ArrowSlot> sourceArrows)
    {
        _arrows.Clear();
        foreach (ArrowSlot source in sourceArrows)
        {
            _arrows.Add(new ArrowSlot(_player)
            {
                Status = source.Status,
                DestroyAfterImpaled = source.DestroyAfterImpaled,
            });
        }
    }

    private readonly List<Creature> _pets = new();

    public IReadOnlyList<Creature> Pets => _pets;
    // Transient async execution depth, scoped to this particular combat state.
    internal int CardOrPotionEffectDepth { get; set; }

    public int Energy { get; set; }

    public int Stars { get; private set; }

    /// <summary>逐字移植：经 Hook.ModifyMaxEnergy 折叠玩家的基础 MaxEnergy。</summary>
    public int MaxEnergy => (int)Hook.ModifyMaxEnergy(_player.Creature.CombatState!, _player, _player.MaxEnergy);

    public PlayerTurnPhase Phase { get; set; }

    public int TurnNumber { get; set; }

    public CardPile Hand { get; } = new(PileType.Hand);

    public CardPile DrawPile { get; } = new(PileType.Draw);

    public CardPile DiscardPile { get; } = new(PileType.Discard);

    public CardPile ExhaustPile { get; } = new(PileType.Exhaust);

    public CardPile PlayPile { get; } = new(PileType.Play);

    public IReadOnlyList<CardPile> AllPiles => new[] { Hand, DrawPile, DiscardPile, ExhaustPile, PlayPile };

    public void ResetEnergy() => Energy = MaxEnergy;

    public void AddMaxEnergyToCurrent() => Energy += MaxEnergy;

    public void GainEnergy(decimal amount) => Energy = (int)Math.Clamp(Energy + amount, 0m, 999999999m);

    public void LoseEnergy(decimal amount) => Energy = (int)Math.Clamp(Energy - amount, 0m, 999999999m);

    public void GainStars(decimal amount)
    {
        if (amount > 0m)
        {
            StarsGainedThisTurn += amount;
        }

        Stars = (int)Math.Max(Stars + amount, 0m);
    }

    public void LoseStars(decimal amount) => Stars = (int)Math.Max(Stars - amount, 0m);

    /// <summary>本回合已获得的星愿量（LunarBlast类"计算值"卡使用）。逐字移植：真实游戏靠战斗历史日志
    /// （<c>CombatManager.History</c>）回查,本项目没有完整历史日志系统,改用逐回合重置的计数器,偏离 #101。</summary>
    public decimal StarsGainedThisTurn { get; private set; }

    /// <summary>本回合已打出的技能牌数（LunarBlast使用）。见 <see cref="StarsGainedThisTurn"/> 的偏离说明。</summary>
    public int SkillCardsPlayedThisTurn { get; private set; }

    /// <summary>Completed Attack plays this turn; a narrow equivalent of combat-history queries.</summary>
    public int AttackCardsPlayedThisTurn { get; private set; }

    /// <summary>Completed Shiv-tagged plays this turn, including plays before a power was applied.</summary>
    public int ShivsPlayedThisTurn { get; private set; }

    /// <summary>CardCmd discard events by this player this turn, including events before a card was generated.</summary>
    public int CardsDiscardedThisTurn { get; private set; }

    /// <summary>本场战斗已打出的卡牌总数（GoldAxe使用，不随回合重置）。</summary>
    public int CardsPlayedThisCombat { get; private set; }

    /// <summary>本场战斗中由该玩家生成的卡牌数（Supermassive使用，不随回合重置）。</summary>
    public int CardsGeneratedThisCombat { get; private set; }

    /// <summary>本场战斗中实际抽到的卡牌数；Murder 对真实 CardDrawnEntry 查询的最小等价状态。</summary>
    public int CardsDrawnThisCombat { get; private set; }

    /// <summary>本回合已打出的卡牌总数（PaleBlueDotPower使用）。见 <see cref="StarsGainedThisTurn"/> 的偏离说明。
    /// 偏离 #106：真实源码用 <c>CombatManager.History.CardPlaysFinished</c> 回查"这张卡是否算在本轮次结算里"，
    /// 本项目没有历史日志，直接按 <see cref="Models.CardModel.PlayAsync"/> 每轮 Replay 结算各计数一次。</summary>
    public int CardsPlayedThisTurn { get; private set; }

    /// <summary>Completed non-autoplay card plays during the current turn.</summary>
    public int ManualCardsPlayedThisTurn { get; private set; }

    public int CardPlaysStartedThisTurn { get; private set; }

    public int AttackOrSkillCardPlaysStartedThisTurn { get; private set; }

    /// <summary>Attack plays started this turn, including a card currently resolving.</summary>
    public int AttackCardsStartedThisTurn { get; private set; }

    /// <summary>Attack starts whose captured energy value was zero, including replay and autoplay.</summary>
    public int ZeroCostAttacksStartedThisTurn { get; private set; }

    /// <summary>Card starts with PlayIndex zero; replay continuations do not count.</summary>
    public int FirstInSeriesCardPlaysStartedThisTurn { get; private set; }

    /// <summary>本回合首个开始打出对应旋律标签时的 PlayOrdinal（0 表示尚未打出）。</summary>
    public int FirstWahPlayOrdinalThisTurn { get; private set; }
    public int FirstChoPlayOrdinalThisTurn { get; private set; }
    public int FirstKiPlayOrdinalThisTurn { get; private set; }

    /// <summary>本场战斗由当前玩家作为生成者生成的 Sadness 次数（原版 <c>Anguish</c> 查询 <c>CardGeneratedEntry</c> 的窄化计数）。</summary>
    public int SadnessGeneratedThisCombat { get; private set; }

    /// <summary>本回合已开始的含对应旋律标签出牌次数（含自动出牌与重放，用于 LostLute / Motif）。</summary>
    public int WahStartedThisTurn { get; private set; }
    public int ChoStartedThisTurn { get; private set; }
    public int KiStartedThisTurn { get; private set; }

    /// <summary>本回合已完成的含对应旋律标签出牌次数（含自动出牌与重放）。</summary>
    public int WahFinishedThisTurn { get; private set; }
    public int ChoFinishedThisTurn { get; private set; }
    public int KiFinishedThisTurn { get; private set; }
    public int MelodyCardsFinishedThisTurn { get; private set; }

    internal void RecordCardPlayStarted(CardPlay cardPlay)
    {
        CardType type = cardPlay.Card.Type;
        RecordCardPlayStarted(type);
        if (type == CardType.Attack && cardPlay.Resources.EnergyValue == 0)
            ZeroCostAttacksStartedThisTurn++;
        if (cardPlay.IsFirstInSeries && ReferenceEquals(cardPlay.Card.Owner, _player))
            FirstInSeriesCardPlaysStartedThisTurn++;
        if (ReferenceEquals(cardPlay.Player, _player))
        {
            bool hasWah = cardPlay.Card.Tags.Contains(CardTag.Wah);
            bool hasCho = cardPlay.Card.Tags.Contains(CardTag.Cho);
            bool hasKi = cardPlay.Card.Tags.Contains(CardTag.Ki);
            if (hasWah)
            {
                WahStartedThisTurn++;
                if (FirstWahPlayOrdinalThisTurn == 0)
                    FirstWahPlayOrdinalThisTurn = cardPlay.PlayOrdinal;
            }
            if (hasCho)
            {
                ChoStartedThisTurn++;
                if (FirstChoPlayOrdinalThisTurn == 0)
                    FirstChoPlayOrdinalThisTurn = cardPlay.PlayOrdinal;
            }
            if (hasKi)
            {
                KiStartedThisTurn++;
                if (FirstKiPlayOrdinalThisTurn == 0)
                    FirstKiPlayOrdinalThisTurn = cardPlay.PlayOrdinal;
            }
        }
    }

    // Existing synthetic fixtures record only the card type; live plays use the CardPlay overload.
    internal void RecordCardPlayStarted(CardType type)
    {
        CardPlaysStartedThisTurn++;
        if (type == CardType.Attack)
            AttackCardsStartedThisTurn++;
        if (type is CardType.Attack or CardType.Skill)
        {
            AttackOrSkillCardPlaysStartedThisTurn++;
        }
    }

    public void RecordCardPlayed(CardType type, bool isAutoPlay = false)
    {
        CardsPlayedThisCombat++;
        CardsPlayedThisTurn++;
        if (!isAutoPlay) ManualCardsPlayedThisTurn++;
        if (type == CardType.Skill)
        {
            SkillCardsPlayedThisTurn++;
        }
        else if (type == CardType.Attack)
        {
            AttackCardsPlayedThisTurn++;
        }
    }

    internal void RecordCardPlayed(CardModel card, bool isAutoPlay)
    {
        RecordCardPlayed(card.Type, isAutoPlay);
        if (card.Tags.Contains(CardTag.Shiv))
        {
            ShivsPlayedThisTurn++;
        }

        bool hasWah = card.Tags.Contains(CardTag.Wah);
        bool hasCho = card.Tags.Contains(CardTag.Cho);
        bool hasKi = card.Tags.Contains(CardTag.Ki);
        if (hasWah) WahFinishedThisTurn++;
        if (hasCho) ChoFinishedThisTurn++;
        if (hasKi) KiFinishedThisTurn++;
        if (hasWah || hasCho || hasKi) MelodyCardsFinishedThisTurn++;
    }

    public void RecordCardGenerated() => CardsGeneratedThisCombat++;

    internal void RecordCardDiscarded() => CardsDiscardedThisTurn++;

    public void RecordCardDrawn() => CardsDrawnThisCombat++;

    internal void RollbackCardGenerated()
    {
        if (CardsGeneratedThisCombat <= 0)
        {
            throw new InvalidOperationException("No generated-card record is available to roll back.");
        }

        CardsGeneratedThisCombat--;
    }

    public void RecordSadnessGenerated() => SadnessGeneratedThisCombat++;

    internal void RollbackSadnessGenerated()
    {
        if (SadnessGeneratedThisCombat <= 0)
        {
            throw new InvalidOperationException("No generated-Sadness record is available to roll back.");
        }

        SadnessGeneratedThisCombat--;
    }


    public void AddPetInternal(Creature pet)
    {
        ArgumentNullException.ThrowIfNull(pet);
        pet.Monster?.AssertMutable();
        if (_pets.Contains(pet))
        {
            return;
        }

        if (!ReferenceEquals(pet.PetOwner, _player))
        {
            pet.PetOwner = _player;
        }
        pet.Died += OnPetDied;
        _pets.Add(pet);
    }

    public Creature? GetPet<T>() where T : MonsterModel =>
        _pets.FirstOrDefault(pet => pet.Monster is T);

    private void OnPetDied(Creature pet)
    {
        if (!_pets.Contains(pet))
        {
            throw new InvalidOperationException("Player does not have the dead pet.");
        }
        ICombatState combatState = pet.CombatState
            ?? throw new InvalidOperationException("Pet died outside its combat state.");
        if (Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(combatState, pet))
        {
            pet.Died -= OnPetDied;
            _pets.Remove(pet);
        }
    }
    /// <summary>清理逐回合计数与窄化的“本回合免费”标记。完整 CardEnergyCost 修饰符链仍未移植（偏离 #32）。</summary>
    public void EndOfTurnCleanup()
    {
        foreach (CardModel card in AllPiles.SelectMany(pile => pile.Cards))
        {
            card.EndOfTurnCleanup();
        }

        ResetTurnCounters();
    }

    /// <summary>清零本回合计数。这些计数对应原版 <c>CombatHistoryEntry.HappenedThisTurn</c>（同时比对
    /// RoundNumber、CurrentSide 与各玩家 TurnNumber）的查询。原版额外回合在 <c>CombatManager.SwitchSides</c>
    /// 递增玩家 TurnNumber，所以额外回合同样从零计数；换边之前（AfterSideTurnEnd 等）记下的事件归属刚结束的回合。
    /// 因此 <see cref="Combat.CombatEngine"/> 在每次换边时再清一次（#1002）。</summary>
    internal void ResetTurnCounters()
    {
        StarsGainedThisTurn = 0m;
        SkillCardsPlayedThisTurn = 0;
        AttackCardsPlayedThisTurn = 0;
        ShivsPlayedThisTurn = 0;
        CardsDiscardedThisTurn = 0;
        CardsPlayedThisTurn = 0;
        ManualCardsPlayedThisTurn = 0;
        AttackOrSkillCardPlaysStartedThisTurn = 0;
        AttackCardsStartedThisTurn = 0;
        ZeroCostAttacksStartedThisTurn = 0;
        FirstInSeriesCardPlaysStartedThisTurn = 0;
        CardPlaysStartedThisTurn = 0;
    }

    /// <summary>在 <c>AfterSideTurnEnd</c> 派发之后、换边或进入下一回合前清零本回合旋律统计，保证回合结束钩子可见。</summary>
    internal void ResetMelodyTurnCounters()
    {
        WahStartedThisTurn = 0;
        ChoStartedThisTurn = 0;
        KiStartedThisTurn = 0;
        FirstWahPlayOrdinalThisTurn = 0;
        FirstChoPlayOrdinalThisTurn = 0;
        FirstKiPlayOrdinalThisTurn = 0;
        WahFinishedThisTurn = 0;
        ChoFinishedThisTurn = 0;
        KiFinishedThisTurn = 0;
        MelodyCardsFinishedThisTurn = 0;
    }

    internal PlayerCombatState CloneForCombat(
        Player clonedPlayer,
        IDictionary<CardModel, CardModel> cardMap)
    {
        var clone = new PlayerCombatState(clonedPlayer)
        {
            Energy = Energy,
            Stars = Stars,
            Phase = Phase,
            TurnNumber = TurnNumber,
            StarsGainedThisTurn = StarsGainedThisTurn,
            SkillCardsPlayedThisTurn = SkillCardsPlayedThisTurn,
            AttackCardsPlayedThisTurn = AttackCardsPlayedThisTurn,
            ShivsPlayedThisTurn = ShivsPlayedThisTurn,
            CardsDiscardedThisTurn = CardsDiscardedThisTurn,
            CardsPlayedThisCombat = CardsPlayedThisCombat,
            CardsGeneratedThisCombat = CardsGeneratedThisCombat,
            SadnessGeneratedThisCombat = SadnessGeneratedThisCombat,
            CardsDrawnThisCombat = CardsDrawnThisCombat,
            CardsPlayedThisTurn = CardsPlayedThisTurn,
            ManualCardsPlayedThisTurn = ManualCardsPlayedThisTurn,
            AttackOrSkillCardPlaysStartedThisTurn = AttackOrSkillCardPlaysStartedThisTurn,
            AttackCardsStartedThisTurn = AttackCardsStartedThisTurn,
            ZeroCostAttacksStartedThisTurn = ZeroCostAttacksStartedThisTurn,
            FirstInSeriesCardPlaysStartedThisTurn = FirstInSeriesCardPlaysStartedThisTurn,
            CardPlaysStartedThisTurn = CardPlaysStartedThisTurn,
            WahStartedThisTurn = WahStartedThisTurn,
            ChoStartedThisTurn = ChoStartedThisTurn,
            KiStartedThisTurn = KiStartedThisTurn,
            FirstWahPlayOrdinalThisTurn = FirstWahPlayOrdinalThisTurn,
            FirstChoPlayOrdinalThisTurn = FirstChoPlayOrdinalThisTurn,
            FirstKiPlayOrdinalThisTurn = FirstKiPlayOrdinalThisTurn,
            WahFinishedThisTurn = WahFinishedThisTurn,
            ChoFinishedThisTurn = ChoFinishedThisTurn,
            KiFinishedThisTurn = KiFinishedThisTurn,
            MelodyCardsFinishedThisTurn = MelodyCardsFinishedThisTurn,
            OrbQueue = OrbQueue.CloneForCombat(clonedPlayer),
        };

        foreach ((CardPile source, CardPile target) in AllPiles.Zip(clone.AllPiles))
        {
            foreach (CardModel card in source.Cards)
            {
                CardModel clonedCard = card.CloneForCombat(clonedPlayer);
                target.AddInternal(clonedCard);
                cardMap.Add(card, clonedCard);
            }
        }

        // 复制完所有战斗牌堆后再精准覆盖箭槽，防止非 Fool 角色克隆 Shoot 牌时重复追加或丢失状态。
        clone.ReplaceClonedArrows(_arrows);
        return clone;
    }
}
