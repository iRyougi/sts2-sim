using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Orbs;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Entities.Players;

/// <summary>玩家的战斗期状态：能量/星愿/回合阶段/牌堆/宠物/充能球。
/// 偏离：Stars 无回合重置逻辑（对照真实源码核实，只由 GainStars/LoseStars 显式增减）。</summary>
public sealed class PlayerCombatState(Player player)
{
    private static OrbQueue NewOrbQueue(Player owner)
    {
        var queue = new OrbQueue(owner);
        queue.Clear();
        queue.AddCapacity(owner.BaseOrbSlotCount);
        return queue;
    }

    public OrbQueue OrbQueue { get; private set; } = NewOrbQueue(player);

    private readonly List<Creature> _pets = new();

    public IReadOnlyList<Creature> Pets => _pets;
    // Transient async execution depth, scoped to this particular combat state.
    internal int CardOrPotionEffectDepth { get; set; }

    public int Energy { get; set; }

    public int Stars { get; private set; }

    /// <summary>逐字移植：经 Hook.ModifyMaxEnergy 折叠玩家的基础 MaxEnergy。</summary>
    public int MaxEnergy => (int)Hook.ModifyMaxEnergy(player.Creature.CombatState!, player, player.MaxEnergy);

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

    internal void RecordCardPlayStarted(CardPlay cardPlay)
    {
        CardType type = cardPlay.Card.Type;
        RecordCardPlayStarted(type);
        if (type == CardType.Attack && cardPlay.Resources.EnergyValue == 0)
            ZeroCostAttacksStartedThisTurn++;
        if (cardPlay.IsFirstInSeries && ReferenceEquals(cardPlay.Card.Owner, player))
            FirstInSeriesCardPlaysStartedThisTurn++;
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


    public void AddPetInternal(Creature pet)
    {
        ArgumentNullException.ThrowIfNull(pet);
        pet.Monster?.AssertMutable();
        if (_pets.Contains(pet))
        {
            return;
        }

        if (!ReferenceEquals(pet.PetOwner, player))
        {
            pet.PetOwner = player;
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
            card.ClearTemporaryFreeThisTurn();
            card.ClearTemporaryRetainThisTurn();
            card.ClearTemporarySlyThisTurn();
            card.ClearTemporaryCostOverrideThisTurn();
            card.ClearTemporaryCostOverrideThisTurnOrUntilPlayed();
            card.ClearTemporaryStarCostOverrideThisTurn();
            card.ClearEnergyCostThisTurn();
            card.ExhaustOnNextPlay = false;
        }

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
            CardsDrawnThisCombat = CardsDrawnThisCombat,
            CardsPlayedThisTurn = CardsPlayedThisTurn,
            ManualCardsPlayedThisTurn = ManualCardsPlayedThisTurn,
            AttackOrSkillCardPlaysStartedThisTurn = AttackOrSkillCardPlaysStartedThisTurn,
            AttackCardsStartedThisTurn = AttackCardsStartedThisTurn,
            ZeroCostAttacksStartedThisTurn = ZeroCostAttacksStartedThisTurn,
            FirstInSeriesCardPlaysStartedThisTurn = FirstInSeriesCardPlaysStartedThisTurn,
            CardPlaysStartedThisTurn = CardPlaysStartedThisTurn,
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

        return clone;
    }
}
