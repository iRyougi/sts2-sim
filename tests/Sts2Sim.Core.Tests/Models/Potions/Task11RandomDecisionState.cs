using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.Random;

namespace Sts2Sim.Core.Tests.Models.Potions;

internal sealed class Task11RandomDecisionState : MonsterState
{
    private readonly MoveState _left;
    private readonly MoveState _right;

    public Task11RandomDecisionState(MoveState left, MoveState right)
    {
        _left = left;
        _right = right;
    }

    public override string Id => "RANDOM";

    public override bool ShouldAppearInLogs => false;

    public override string GetNextState(Creature owner, Rng rng) =>
        rng.NextBool() ? _left.Id : _right.Id;

    public override void RegisterStates(Dictionary<string, MonsterState> monsterStates)
    {
        monsterStates.Add(Id, this);
        _left.RegisterStates(monsterStates);
        _right.RegisterStates(monsterStates);
    }
}
