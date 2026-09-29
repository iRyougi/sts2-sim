namespace Sts2Sim.Core.MonsterMoves;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Random;

/// <summary>Checks predicates in order and transitions to the first matching target state.</summary>
public sealed class ConditionalBranchState : MonsterState
{
    private readonly List<(Func<Creature, bool> Predicate, string TargetStateId)> _branches = new();

    public ConditionalBranchState(string id) => Id = id;

    public override string Id { get; }

    public override bool IsMove => false;

    public override bool ShouldAppearInLogs => false;

    public ConditionalBranchState AddBranch(Func<Creature, bool> predicate, string targetStateId)
    {
        _branches.Add((predicate, targetStateId));
        return this;
    }

    public override string GetNextState(Creature owner, Rng rng)
    {
        foreach ((Func<Creature, bool> predicate, string targetStateId) in _branches)
        {
            if (predicate(owner))
            {
                return targetStateId;
            }
        }

        throw new InvalidOperationException($"No branch predicate matched for ConditionalBranchState '{Id}'.");
    }

    public override void RegisterStates(Dictionary<string, MonsterState> monsterStates) => monsterStates.Add(Id, this);
}
