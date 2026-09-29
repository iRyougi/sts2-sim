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
        return _cardBlockGains.Count(entry =>
            entry.ActorIndex == actorIndex &&
            entry.Turn.HappenedThisTurn(state) &&
            (currentPlay is null ||
             entry.PlayOrdinal != currentPlay.PlayOrdinal));
    }

    internal CombatSemanticHistory Clone()
    {
        var clone = new CombatSemanticHistory();
        clone._exhausts.AddRange(_exhausts.Select(entry => entry with
        {
            Turn = entry.Turn.Copy(),
        }));
        clone._cardBlockGains.AddRange(_cardBlockGains.Select(entry => entry with
        {
            Turn = entry.Turn.Copy(),
        }));
        clone._orbChannels.AddRange(_orbChannels.Select(entry => entry with
        {
            Turn = entry.Turn.Copy(),
        }));
        clone._energySpent.AddRange(_energySpent.Select(entry => entry with
        {
            Turn = entry.Turn.Copy(),
        }));
        clone._cardDrawn.AddRange(_cardDrawn.Select(entry => entry with
        {
            Turn = entry.Turn.Copy(),
        }));
        clone._actorEvents.AddRange(_actorEvents.Select(entry => entry with
        {
            Turn = entry.Turn.Copy(),
        }));
        return clone;
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

    private static TurnKey CaptureTurnKey(CombatState state) => new(
        state.RoundNumber,
        state.CurrentSide,
        state.Players.Select(player => player.PlayerCombatState?.TurnNumber).ToArray());

    private readonly record struct ExhaustEntry(TurnKey Turn, int ActorIndex);

    private readonly record struct BlockGainEntry(TurnKey Turn, int ActorIndex, int PlayOrdinal);

    private readonly record struct OrbChannelEntry(TurnKey Turn, int ActorIndex, ModelId OrbId);

    private readonly record struct EnergySpentEntry(TurnKey Turn, int ActorIndex, int Amount);

    private readonly record struct CardDrawnEntry(
        TurnKey Turn, int ActorIndex, CardType CardType, bool FromHandDraw);

    private readonly record struct ActorEventEntry(ActorEvent Kind, TurnKey Turn, int ActorIndex);

    private readonly record struct TurnKey(
        int RoundNumber, CombatSide Side, int?[] PlayerTurnNumbers)
    {
        public bool HappenedThisTurn(CombatState state)
        {
            if (RoundNumber != state.RoundNumber || Side != state.CurrentSide ||
                PlayerTurnNumbers.Length != state.Players.Count)
                return false;
            for (int i = 0; i < PlayerTurnNumbers.Length; i++)
            {
                if (state.Players[i].PlayerCombatState?.TurnNumber is not int turnNumber ||
                    PlayerTurnNumbers[i] != turnNumber)
                    return false;
            }
            return true;
        }

        public TurnKey Copy() => this with
        {
            PlayerTurnNumbers = PlayerTurnNumbers.ToArray(),
        };

        public void Append(ref CombatStateDescriptionBuilder builder)
        {
            builder.Append(RoundNumber);
            builder.Append((int)Side);
            builder.Append(PlayerTurnNumbers.Length);
            foreach (int? turnNumber in PlayerTurnNumbers)
            {
                builder.Append(turnNumber.HasValue);
                if (turnNumber is int value)
                    builder.Append(value);
            }
        }
    }
}
