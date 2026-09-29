using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Runs;

/// <summary>A single combat choice: play a card, optionally with a target, or end the turn.</summary>
public abstract record CombatDecision
{
    public sealed record PlayCard(CardModel Card, Creature? Target) : CombatDecision;

    public sealed record UsePotion(PotionModel Potion, Creature? Target) : CombatDecision;
    public sealed record EndTurn : CombatDecision;
}
