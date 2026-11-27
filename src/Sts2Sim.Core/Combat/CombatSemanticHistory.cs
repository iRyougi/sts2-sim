using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Combat;

/// <summary>
/// The native combat history facts needed by Ironclad's Midnight, EvilEye and Unmovable and by Necrobinder content.
/// Entries retain stable player indices and play ordinals so speculative clones have no
/// references to source players or card plays.
/// </summary>
public sealed class CombatSemanticHistory
{
    /// <summary>亡灵契约师内容读取的原版历史事实，按玩家与回合记录。</summary>
    public enum ActorEvent
    {
        /// <summary>原版 <c>CreatureAttackedEntry</c>，攻击者是该玩家的 Osty（Flatten、Rattle）。</summary>
        OstyAttack,
        /// <summary>原版 <c>PowerReceivedEntry</c>，能力是 Doom、施加者是该玩家（DeathsDoor）。</summary>
        DoomApplied,
        /// <summary>原版 <c>CardPlayFinishedEntry.WasEthereal</c>（BansheesCry、PullFromBelow）。</summary>
        EtherealPlayFinished,
        /// <summary>原版 <c>CardAfflictedEntry</c>，苦痛是 Bound、Actor 是牌主（ChainsOfBindingPower 按本回合条数限量）。</summary>
        BoundAfflicted,
    }

    private readonly List<ExhaustEntry> _exhausts = [];
    private readonly List<BlockGainEntry> _cardBlockGains = [];
    private readonly List<OrbChannelEntry> _orbChannels = [];
    private readonly List<EnergySpentEntry> _energySpent = [];
    private readonly List<CardDrawnEntry> _cardDrawn = [];
    private readonly List<ActorEventEntry> _actorEvents = [];

    internal void Record(CombatState state, ActorEvent kind, Player actor) =>
        _actorEvents.Add(new ActorEventEntry(kind, CaptureTurnKey(state), PlayerIndex(state, actor)));

    public int CountThisTurn(CombatState state, ActorEvent kind, Player actor)
    {
        int actorIndex = PlayerIndex(state, actor);
        return _actorEvents.Count(entry => entry.Kind == kind && entry.ActorIndex == actorIndex &&
            entry.Turn.HappenedThisTurn(state));
    }

    public int CountThisCombat(CombatState state, ActorEvent kind, Player actor)
    {
        int actorIndex = PlayerIndex(state, actor);
        return _actorEvents.Count(entry => entry.Kind == kind && entry.ActorIndex == actorIndex);
    }

    public int CardsExhaustedThisCombat => _exhausts.Count;

    internal void RecordExhaust(CombatState state, Player owner)
    {
        _exhausts.Add(new ExhaustEntry(
            CaptureTurnKey(state), PlayerIndex(state, owner)));
    }

    internal void RecordBlockGain(
        CombatState state, ValueProp props, CardPlay? cardPlay)
    {
        // Unmovable reads only BlockGainedEntry records with a CardPlay and Move prop.
        if (cardPlay is null || !props.HasFlag(ValueProp.Move))
            return;
        _cardBlockGains.Add(new BlockGainEntry(
            CaptureTurnKey(state), PlayerIndex(state, cardPlay.Player),
            cardPlay.PlayOrdinal));
    }

    internal void RecordOrbChanneled(CombatState state, OrbModel orb)
    {
        _orbChannels.Add(new OrbChannelEntry(
            CaptureTurnKey(state), PlayerIndex(state, orb.Owner), orb.Id));
    }

    internal void RecordEnergySpent(CombatState state, Player actor, int amount)
    {
        if (amount <= 0)
            return;
        _energySpent.Add(new EnergySpentEntry(
            CaptureTurnKey(state), PlayerIndex(state, actor), amount));
    }

    internal void RecordCardDrawn(CombatState state, CardModel card, bool fromHandDraw)
    {
        _cardDrawn.Add(new CardDrawnEntry(
            CaptureTurnKey(state), PlayerIndex(state, card.Owner), card.Type, fromHandDraw));
    }

    public int SumEnergySpentThisTurn(CombatState state, Player actor)
    {
        int actorIndex = PlayerIndex(state, actor);
        return _energySpent.Where(entry => entry.ActorIndex == actorIndex &&
            entry.Turn.HappenedThisTurn(state)).Sum(entry => entry.Amount);
    }

    /// <summary>原版 DeathMarch：本回合该玩家在回合抽牌之外抽到的牌数。</summary>
    public int CountCardsDrawnOutsideHandDrawThisTurn(CombatState state, Player actor)
    {
        int actorIndex = PlayerIndex(state, actor);
        return _cardDrawn.Count(entry => entry.ActorIndex == actorIndex &&
            !entry.FromHandDraw && entry.Turn.HappenedThisTurn(state));
    }

    public int CountStatusCardsDrawnThisTurn(CombatState state, Player actor)
    {
        int actorIndex = PlayerIndex(state, actor);
        return _cardDrawn.Count(entry => entry.ActorIndex == actorIndex &&
            entry.CardType == CardType.Status && entry.Turn.HappenedThisTurn(state));
    }

    public int CountOrbsChanneledThisCombat<T>(CombatState state, Player actor)
        where T : OrbModel =>
        _orbChannels.Count(entry =>
            entry.ActorIndex == PlayerIndex(state, actor) &&
            entry.OrbId == ModelDb.GetId<T>());

    public int CountCardsExhaustedThisTurn(CombatState state, Player actor)
    {
        int actorIndex = PlayerIndex(state, actor);
        return _exhausts.Count(entry => entry.ActorIndex == actorIndex &&
            entry.Turn.HappenedThisTurn(state));
    }

    public int CountOtherQualifyingBlockGainsThisTurn(
        CombatState state, Creature owner, CardPlay? currentPlay)
    {
        if (owner.Player is not { } player)
            return 0;
        int actorIndex = PlayerIndex(state, player);
        // v0.114.0 (#1110): native UnmovablePower counts distinct CardPlays (.DistinctBy(e => e.CardPlay)), so one card
        // play that gains block several times (a multi-hit or repeated gain) counts once.
        return _cardBlockGains
            .Where(entry =>
                entry.ActorIndex == actorIndex &&
                entry.Turn.HappenedThisTurn(state) &&
                (currentPlay is null ||
                 entry.PlayOrdinal != currentPlay.PlayOrdinal))
            .Select(entry => entry.PlayOrdinal)
            .Distinct()
            .Count();
    }

    internal CombatSemanticHistory Clone()
    {
        var clone = new CombatSemanticHistory();
        CopyEntries(_exhausts, clone._exhausts, static e => e with { Turn = e.Turn.Copy() });
        CopyEntries(_cardBlockGains, clone._cardBlockGains, static e => e with { Turn = e.Turn.Copy() });
        CopyEntries(_orbChannels, clone._orbChannels, static e => e with { Turn = e.Turn.Copy() });
        CopyEntries(_energySpent, clone._energySpent, static e => e with { Turn = e.Turn.Copy() });
        CopyEntries(_cardDrawn, clone._cardDrawn, static e => e with { Turn = e.Turn.Copy() });
        CopyEntries(_actorEvents, clone._actorEvents, static e => e with { Turn = e.Turn.Copy() });
        return clone;
    }

    private static void CopyEntries<TEntry>(List<TEntry> source, List<TEntry> target, Func<TEntry, TEntry> copy)
    {
        if (source.Count == 0)
        {
            return;
        }
        target.EnsureCapacity(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            target.Add(copy(source[i]));
        }
    }

    internal void AppendStateDescription(ref CombatStateDescriptionBuilder builder)
    {
        builder.Append(_exhausts.Count);
        foreach (ExhaustEntry entry in _exhausts)
        {
            entry.Turn.Append(ref builder);
            builder.Append(entry.ActorIndex);
        }

        builder.Append(_cardBlockGains.Count);
        foreach (BlockGainEntry entry in _cardBlockGains)
        {
            entry.Turn.Append(ref builder);
            builder.Append(entry.ActorIndex);
            builder.Append(entry.PlayOrdinal);
        }

        builder.Append(_orbChannels.Count);
        foreach (OrbChannelEntry entry in _orbChannels)
        {
            entry.Turn.Append(ref builder);
            builder.Append(entry.ActorIndex);
            builder.Append(entry.OrbId);
        }

        builder.Append(_energySpent.Count);
        foreach (EnergySpentEntry entry in _energySpent)
        {
            entry.Turn.Append(ref builder);
            builder.Append(entry.ActorIndex);
            builder.Append(entry.Amount);
        }

        builder.Append(_cardDrawn.Count);
        foreach (CardDrawnEntry entry in _cardDrawn)
        {
            entry.Turn.Append(ref builder);
            builder.Append(entry.ActorIndex);
            builder.Append((int)entry.CardType);
            builder.Append(entry.FromHandDraw);
        }

        builder.Append(_actorEvents.Count);
        foreach (ActorEventEntry entry in _actorEvents)
        {
            builder.Append((int)entry.Kind);
            entry.Turn.Append(ref builder);
            builder.Append(entry.ActorIndex);
        }
    }

    private static int PlayerIndex(CombatState state, Player player)
    {
        for (int i = 0; i < state.Players.Count; i++)
            if (ReferenceEquals(state.Players[i], player))
                return i;
        throw new InvalidOperationException("Combat history actor is not in this combat.");
    }

    private static TurnKey CaptureTurnKey(CombatState state)
    {
        int count = state.Players.Count;
        if (count == 0)
        {
            return new TurnKey(state.RoundNumber, state.CurrentSide, 0, null, null);
        }
        int? p0 = state.Players[0].PlayerCombatState?.TurnNumber;
        if (count == 1)
        {
            return new TurnKey(state.RoundNumber, state.CurrentSide, 1, p0, null);
        }
        var extra = new int?[count - 1];
        for (int i = 1; i < count; i++)
        {
            extra[i - 1] = state.Players[i].PlayerCombatState?.TurnNumber;
        }
        return new TurnKey(state.RoundNumber, state.CurrentSide, count, p0, extra);
    }

    private readonly record struct ExhaustEntry(TurnKey Turn, int ActorIndex);

    private readonly record struct BlockGainEntry(TurnKey Turn, int ActorIndex, int PlayOrdinal);

    private readonly record struct OrbChannelEntry(TurnKey Turn, int ActorIndex, ModelId OrbId);

    private readonly record struct EnergySpentEntry(TurnKey Turn, int ActorIndex, int Amount);

    private readonly record struct CardDrawnEntry(
        TurnKey Turn, int ActorIndex, CardType CardType, bool FromHandDraw);

    private readonly record struct ActorEventEntry(ActorEvent Kind, TurnKey Turn, int ActorIndex);

    private readonly record struct TurnKey(
        int RoundNumber, CombatSide Side, int PlayerCount, int? Player0TurnNumber, int?[]? ExtraPlayerTurnNumbers)
    {
        public bool HappenedThisTurn(CombatState state)
        {
            if (RoundNumber != state.RoundNumber || Side != state.CurrentSide ||
                PlayerCount != state.Players.Count)
                return false;
            if (PlayerCount == 0)
                return true;
            if (state.Players[0].PlayerCombatState?.TurnNumber is not int turn0 ||
                Player0TurnNumber != turn0)
                return false;
            for (int i = 1; i < PlayerCount; i++)
            {
                if (state.Players[i].PlayerCombatState?.TurnNumber is not int turnNumber ||
                    ExtraPlayerTurnNumbers![i - 1] != turnNumber)
                    return false;
            }
            return true;
        }

        public TurnKey Copy() => ExtraPlayerTurnNumbers is null
            ? this
            : this with { ExtraPlayerTurnNumbers = ExtraPlayerTurnNumbers.ToArray() };

        public void Append(ref CombatStateDescriptionBuilder builder)
        {
            builder.Append(RoundNumber);
            builder.Append((int)Side);
            builder.Append(PlayerCount);
            if (PlayerCount > 0)
            {
                builder.Append(Player0TurnNumber.HasValue);
                if (Player0TurnNumber is int v0)
                    builder.Append(v0);
                for (int i = 1; i < PlayerCount; i++)
                {
                    int? turnNumber = ExtraPlayerTurnNumbers![i - 1];
                    builder.Append(turnNumber.HasValue);
                    if (turnNumber is int value)
                        builder.Append(value);
                }
            }
        }
    }
}
