using Sts2Sim.Core.Combat.StateDescription;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>A resolved damage event with the turn snapshot needed by dynamic card formulas.</summary>
public sealed class CombatDamageHistoryEntry
{
    private readonly int _playerCount;
    private readonly int _player0TurnNumber;
    private readonly int[]? _extraPlayerTurnNumbers;

    internal CombatDamageHistoryEntry(
        DamageResult result,
        Creature receiver,
        Creature? dealer,
        CardModel? cardSource,
        ICombatState combatState)
    {
        Result = result;
        Receiver = receiver;
        Dealer = dealer;
        CardSource = cardSource;
        RoundNumber = combatState.RoundNumber;
        CurrentSide = combatState.CurrentSide;
        IReadOnlyList<Player> players = combatState.Players;
        _playerCount = players.Count;
        if (_playerCount > 0)
        {
            _player0TurnNumber = players[0].PlayerCombatState!.TurnNumber;
            if (_playerCount > 1)
            {
                var extra = new int[_playerCount - 1];
                for (int i = 1; i < _playerCount; i++)
                {
                    extra[i - 1] = players[i].PlayerCombatState!.TurnNumber;
                }
                _extraPlayerTurnNumbers = extra;
            }
        }
    }

    private CombatDamageHistoryEntry(
        DamageResult result,
        Creature receiver,
        Creature? dealer,
        CardModel? cardSource,
        int roundNumber,
        CombatSide currentSide,
        int playerCount,
        int player0TurnNumber,
        int[]? extraPlayerTurnNumbers)
    {
        Result = result;
        Receiver = receiver;
        Dealer = dealer;
        CardSource = cardSource;
        RoundNumber = roundNumber;
        CurrentSide = currentSide;
        _playerCount = playerCount;
        _player0TurnNumber = player0TurnNumber;
        _extraPlayerTurnNumbers = extraPlayerTurnNumbers;
    }

    public DamageResult Result { get; }

    public Creature Receiver { get; }

    public Creature? Dealer { get; }

    public CardModel? CardSource { get; }

    public int RoundNumber { get; }

    public CombatSide CurrentSide { get; }

    internal void AppendPlayerTurnNumbers(ref CombatStateDescriptionBuilder builder)
    {
        builder.Append(_playerCount);
        if (_playerCount > 0)
        {
            builder.Append(_player0TurnNumber);
            for (int i = 1; i < _playerCount; i++)
            {
                builder.Append(_extraPlayerTurnNumbers![i - 1]);
            }
        }
    }

    public bool HappenedThisTurn(ICombatState combatState)
    {
        if (RoundNumber != combatState.RoundNumber ||
            CurrentSide != combatState.CurrentSide ||
            _playerCount != combatState.Players.Count)
        {
            return false;
        }

        if (_playerCount == 0)
        {
            return true;
        }

        if (_player0TurnNumber != combatState.Players[0].PlayerCombatState!.TurnNumber)
        {
            return false;
        }

        for (int i = 1; i < _playerCount; i++)
        {
            if (_extraPlayerTurnNumbers![i - 1] != combatState.Players[i].PlayerCombatState!.TurnNumber)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Native last-player-turn query compares only that player's turn number.</summary>
    public bool HappenedLastPlayerTurn(Player player)
    {
        if (player.PlayerCombatState is not { } playerCombatState)
            return false;
        IReadOnlyList<Player> players = player.RunState.Players;
        for (int i = 0; i < players.Count && i < _playerCount; i++)
        {
            if (ReferenceEquals(players[i], player))
            {
                int recorded = i == 0 ? _player0TurnNumber : _extraPlayerTurnNumbers![i - 1];
                return recorded == playerCombatState.TurnNumber - 1;
            }
        }
        return false;
    }

    internal CombatDamageHistoryEntry Clone(
        IReadOnlyDictionary<Creature, Creature> creatureMap,
        Dictionary<CardModel, CardModel> cardMap,
        ref List<CardModel>? newlyAddedSources)
    {
        Creature receiver = creatureMap[Receiver];
        Creature? dealer = Dealer is null ? null : creatureMap[Dealer];
        CardModel? cardSource = null;
        if (CardSource is not null && !cardMap.TryGetValue(CardSource, out cardSource))
        {
            cardSource = CardSource.CloneForCombat(
                CardSource.Owner is { } owner &&
                creatureMap.TryGetValue(owner.Creature, out Creature? cloneOwner)
                    ? cloneOwner.Player ??
                      throw new InvalidOperationException("Detached card source owner was not rebound to a player.")
                    : null);
            cardMap.Add(CardSource, cardSource);
            (newlyAddedSources ??= new List<CardModel>(2)).Add(CardSource);
        }

        var result = new DamageResult(receiver, Result.Props)
        {
            OriginalTarget = creatureMap[Result.OriginalTarget],
            IntendedHpLoss = Result.IntendedHpLoss,
            BlockedDamage = Result.BlockedDamage,
            UnblockedDamage = Result.UnblockedDamage,
            OverkillDamage = Result.OverkillDamage,
            WasFullyBlocked = Result.WasFullyBlocked,
            WasTargetKilled = Result.WasTargetKilled,
            WasBlockBroken = Result.WasBlockBroken,
        };
        return new CombatDamageHistoryEntry(
            result,
            receiver,
            dealer,
            cardSource,
            RoundNumber,
            CurrentSide,
            _playerCount,
            _player0TurnNumber,
            _extraPlayerTurnNumbers?.ToArray());
    }
}
