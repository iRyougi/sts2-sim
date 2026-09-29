namespace Sts2Sim.Core.MonsterMoves;

using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Random;

/// <summary>Weighted random branch between monster move states.</summary>
public sealed class RandomBranchState : MonsterState
{
    public struct StateWeight
    {
        public string StateId;
        public MoveRepeatType RepeatType;
        public int MaxTimes;
        public Func<float>? Weight;
        public int Cooldown;

        public float GetWeight() => Weight?.Invoke() ?? throw new InvalidOperationException(StateId + " doesn't have a weight");
    }

    public RandomBranchState(string id)
    {
        Id = id;
    }

    public override string Id { get; }
    public List<StateWeight> States { get; set; } = new();
    public override bool ShouldAppearInLogs => false;
    public override bool IsMove => false;

    public void AddBranch(MonsterState state, int cooldown, MoveRepeatType repeatType, Func<float> weight)
    {
        if (repeatType == MoveRepeatType.CanRepeatXTimes)
        {
            throw new ArgumentException("Use other constructor to specify number of repeats");
        }

        States.Add(new StateWeight { RepeatType = repeatType, StateId = state.Id, Weight = weight, Cooldown = cooldown });
    }

    public void AddBranch(MonsterState state, int cooldown, int maxRepeats, Func<float> weight)
    {
        States.Add(new StateWeight { MaxTimes = maxRepeats, RepeatType = MoveRepeatType.CanRepeatXTimes, StateId = state.Id, Weight = weight, Cooldown = cooldown });
    }

    public void AddBranch(MonsterState state, int maxRepeats, Func<float> weight) => AddBranch(state, 0, maxRepeats, weight);
    public void AddBranch(MonsterState state, int cooldown, MoveRepeatType repeatType, float weight) => AddBranch(state, cooldown, repeatType, () => weight);
    public void AddBranch(MonsterState state, MoveRepeatType repeatType, float weight) => AddBranch(state, repeatType, () => weight);
    public void AddBranch(MonsterState state, MoveRepeatType repeatType, Func<float> weight) => AddBranch(state, 0, repeatType, weight);
    public void AddBranch(MonsterState state, int maxRepeats, float weight) => AddBranch(state, maxRepeats, () => weight);
    public void AddBranch(MonsterState state, int cooldown, MoveRepeatType repeatType) => AddBranch(state, cooldown, repeatType, 1f);
    public void AddBranch(MonsterState state, int maxRepeats) => AddBranch(state, maxRepeats, 1f);
    public void AddBranch(MonsterState state, MoveRepeatType repeatType) => AddBranch(state, repeatType, 1f);

    public override string GetNextState(Creature owner, Rng rng)
    {
        float totalWeight = States.Sum(state => GetStateWeight(state, owner));
        float randomWeight = rng.NextFloat(totalWeight);
        foreach (StateWeight state in States)
        {
            randomWeight -= GetStateWeight(state, owner);
            if (randomWeight <= 0f)
            {
                return state.StateId;
            }
        }

        throw new InvalidOperationException("No valid state found in RandomBranchState " + Id + "!");
    }

    public override void RegisterStates(Dictionary<string, MonsterState> monsterStates) => monsterStates.Add(Id, this);

    private static float GetStateWeight(StateWeight stateWeight, Creature owner)
    {
        MonsterMoveStateMachine moveStateMachine = owner.Monster!.MoveStateMachine!;
        float repeatWeight = 1f;
        if (stateWeight.RepeatType == MoveRepeatType.UseOnlyOnce)
        {
            MonsterState state = moveStateMachine.States[stateWeight.StateId];
            if (moveStateMachine.StateLog.Contains(state))
            {
                repeatWeight = 0f;
            }
        }
        else if (stateWeight.RepeatType != MoveRepeatType.CanRepeatForever)
        {
            float repeatLimit = stateWeight.RepeatType == MoveRepeatType.CannotRepeat ? 1 : stateWeight.MaxTimes;
            repeatWeight = moveStateMachine.StateLog.Count < repeatLimit ? 1f : 0f;
            int checkedCount = 0;
            while (moveStateMachine.StateLog.Count >= repeatLimit && checkedCount < repeatLimit && moveStateMachine.StateLog.Count - checkedCount > 0)
            {
                MonsterState state = moveStateMachine.States[stateWeight.StateId];
                if (moveStateMachine.StateLog[moveStateMachine.StateLog.Count - 1 - checkedCount] != state)
                {
                    repeatWeight = 1f;
                    break;
                }

                checkedCount++;
            }
        }

        if (stateWeight.Cooldown > 0)
        {
            IEnumerable<MonsterState> recentMoves = moveStateMachine.StateLog.Where(state => state.IsMove).Reverse().Take(stateWeight.Cooldown);
            if (recentMoves.Any(move => move.Id == stateWeight.StateId))
            {
                return 0f;
            }
        }

        return repeatWeight * stateWeight.GetWeight();
    }
}
