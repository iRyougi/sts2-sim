using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>A resolved damage event with the turn snapshot needed by dynamic card formulas.</summary>
public sealed class CombatDamageHistoryEntry
{
    private readonly int[] _playerTurnNumbers;

    internal CombatDamageHistoryEntry(
        DamageResult result,
        Creature receiver,
        Creature? dealer,
        CardModel? cardSource,
        ICombatState combatState)
        : this(
            result,
            receiver,
            dealer,
            cardSource,
            combatState.RoundNumber,
            combatState.CurrentSide,
            combatState.Players.Select(player => player.PlayerCombatState!.TurnNumber).ToArray())
    {
    }

    private CombatDamageHistoryEntry(
        DamageResult result,
        Creature receiver,
        Creature? dealer,
        CardModel? cardSource,
        int roundNumber,
        CombatSide currentSide,
        int[] playerTurnNumbers)
    {
        Result = result;
        Receiver = receiver;
        Dealer = dealer;
        CardSource = cardSource;
        RoundNumber = roundNumber;
        CurrentSide = currentSide;
        _playerTurnNumbers = playerTurnNumbers;
    }

    public DamageResult Result { get; }

    public Creature Receiver { get; }

    public Creature? Dealer { get; }

    public CardModel? CardSource { get; }

    public int RoundNumber { get; }

    public CombatSide CurrentSide { get; }

    internal IReadOnlyList<int> PlayerTurnNumbers => _playerTurnNumbers;

    public bool HappenedThisTurn(ICombatState combatState)
    {
        if (RoundNumber != combatState.RoundNumber ||
            CurrentSide != combatState.CurrentSide ||
            _playerTurnNumbers.Length != combatState.Players.Count)
        {
            return false;
        }

        for (int i = 0; i < _playerTurnNumbers.Length; i++)
        {
            if (_playerTurnNumbers[i] != combatState.Players[i].PlayerCombatState!.TurnNumber)
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
        for (int i = 0; i < players.Count && i < _playerTurnNumbers.Length; i++)
            if (ReferenceEquals(players[i], player))
                return _playerTurnNumbers[i] == playerCombatState.TurnNumber - 1;
        return false;
    }

    internal CombatDamageHistoryEntry Clone(
        IReadOnlyDictionary<Creature, Creature> creatureMap,
        Dictionary<CardModel, CardModel> cardMap)
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
        }

        var result = new DamageResult(receiver, Result.Props)
        {
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
            _playerTurnNumbers.ToArray());
    }
}
