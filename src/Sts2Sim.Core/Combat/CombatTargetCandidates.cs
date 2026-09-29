using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat;

/// <summary>Stable, production-wide target ordering for targeted card decisions.</summary>
public static class CombatTargetCandidates
{
    public sealed record PlayerCardTarget(int HandIndex, CardModel Card, Creature Target);

    public static IReadOnlyList<Creature> ForCard(
        ICombatState state,
        Player owner,
        TargetType targetType) => targetType switch
    {
        TargetType.AnyEnemy => state.HittableEnemies,
        TargetType.AnyAlly => state.Allies
            .Where(creature => creature.IsPlayer &&
                               creature.IsAlive &&
                               !ReferenceEquals(creature, owner.Creature))
            .ToList(),
        TargetType.AnyPlayer => state.Allies
            .Where(creature => creature.IsPlayer && creature.IsAlive)
            .ToList(),
        _ => Array.Empty<Creature>(),
    };

    public static IReadOnlyList<PlayerCardTarget> BuildPlayerCardTargets(
        CombatState state,
        Player owner)
    {
        var result = new List<PlayerCardTarget>();
        IReadOnlyList<CardModel> hand = owner.PlayerCombatState!.Hand.Cards;
        for (int handIndex = 0; handIndex < hand.Count; handIndex++)
        {
            CardModel card = hand[handIndex];
            if (card.TargetType is not (TargetType.AnyAlly or TargetType.AnyPlayer) ||
                !card.CanPlay(out _))
            {
                continue;
            }
            foreach (Creature target in ForCard(state, owner, card.TargetType))
            {
                result.Add(new PlayerCardTarget(handIndex, card, target));
            }
        }
        return result;
    }

    public static bool IsLegalCardTarget(
        ICombatState state,
        Player owner,
        CardModel card,
        Creature? target) =>
        target is not null && ForCard(state, owner, card.TargetType).Contains(target);
}
