using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>Old-to-new identity map produced by one <see cref="CombatState.Clone(out CombatCloneMap)"/> call.
/// Read-only; callers must not retain it beyond rebinding their own references.</summary>
public sealed class CombatCloneMap
{
    private readonly IReadOnlyDictionary<CardModel, CardModel> _cards;
    private readonly IReadOnlyDictionary<Player, Player> _players;
    private readonly IReadOnlyDictionary<Creature, Creature> _creatures;

    internal CombatCloneMap(
        IReadOnlyDictionary<CardModel, CardModel> cards,
        IReadOnlyDictionary<Player, Player> players,
        IReadOnlyDictionary<Creature, Creature> creatures)
    {
        _cards = cards;
        _players = players;
        _creatures = creatures;
    }

    public IEnumerable<KeyValuePair<CardModel, CardModel>> Cards => _cards;
    public IEnumerable<KeyValuePair<Player, Player>> Players => _players;
    public IEnumerable<KeyValuePair<Creature, Creature>> Creatures => _creatures;

    public CardModel Card(CardModel source) => _cards.TryGetValue(source, out CardModel? target)
        ? target : throw new KeyNotFoundException("Card is not part of the cloned combat graph.");
    public bool TryCard(CardModel source, out CardModel target) => _cards.TryGetValue(source, out target!);
    public Player Player(Player source) => _players.TryGetValue(source, out Player? target)
        ? target : throw new KeyNotFoundException("Player is not part of the cloned combat.");
    public Creature Creature(Creature source) => _creatures.TryGetValue(source, out Creature? target)
        ? target : throw new KeyNotFoundException("Creature is not part of the cloned combat.");
    public bool TryCreature(Creature source, out Creature target) => _creatures.TryGetValue(source, out target!);
}
