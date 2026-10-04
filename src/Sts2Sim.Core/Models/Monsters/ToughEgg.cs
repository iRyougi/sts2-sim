namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class ToughEgg : MonsterModel
{
    private bool _isHatched;
    private MonsterState? _afterHatchedState;

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 15, 14);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 19, 18);
    public int HatchlingMinHp => Ascension(AscensionLevel.ToughEnemies, 20, 19);
    public int HatchlingMaxHp => Ascension(AscensionLevel.ToughEnemies, 23, 22);
    private int NibbleDamage => Ascension(AscensionLevel.DeadlyEnemies, 5, 4);

    public bool IsHatched
    {
        get => _isHatched;
        set
        {
            AssertMutable();
            _isHatched = value;
        }
    }

    public MonsterState? AfterHatchedState
    {
        get => _afterHatchedState;
        set
        {
            AssertMutable();
            _afterHatchedState = value;
        }
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (!IsHatched)
        {
            int countdown = Creature.CombatState!.CurrentSide == CombatSide.Enemy ? 2 : 1;
            await PowerCmd.Apply<HatchPower>(Creature.CombatState, Creature, countdown, Creature, null);
        }
        else
        {
            await Hatch();
            MoveStateMachine?.ForceCurrentState(AfterHatchedState!);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var hatch = new MoveState("HATCH_MOVE", HatchMove, new SummonIntent());
        var nibble = new MoveState("NIBBLE_MOVE", Nibble, new SingleAttackIntent(NibbleDamage));
        hatch.FollowUpState = nibble;
        nibble.FollowUpState = nibble;
        AfterHatchedState = nibble;
        return new MonsterMoveStateMachine(new MonsterState[] { hatch, nibble }, hatch);
    }

    private async Task HatchMove(IReadOnlyList<Creature> targets)
    {
        IsHatched = true;
        if (Creature.GetPower<HatchPower>() is { } hatchPower)
        {
            await PowerCmd.Remove(hatchPower);
        }

        foreach (PowerModel power in Creature.Powers.Where(power => power is not MinionPower).ToList())
        {
            await PowerCmd.Remove(power);
        }

        await Hatch();
    }

    private async Task Hatch()
    {
        int hp = RunRng.Niche.NextInt(HatchlingMinHp, HatchlingMaxHp + 1);
        await CreatureCmd.SetMaxAndCurrentHp(Creature, hp);
    }

    private Task Nibble(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(NibbleDamage).FromMonster(this).Execute();

    internal override void RestoreCombatCloneReferencesFrom(
        MonsterModel source,
        IReadOnlyDictionary<Creature, Creature> creatureMap)
    {
        var egg = (ToughEgg)source;
        if (egg._afterHatchedState is not { } savedState)
        {
            _afterHatchedState = null;
            return;
        }

        if (egg.MoveStateMachine is null ||
            !egg.MoveStateMachine.States.TryGetValue(savedState.Id, out MonsterState? sourceState) ||
            !ReferenceEquals(sourceState, savedState) ||
            MoveStateMachine is null ||
            !MoveStateMachine.States.TryGetValue(savedState.Id, out MonsterState? clonedState))
        {
            throw new InvalidOperationException(
                $"ToughEgg saved state {savedState.Id} is not present in its combat state graph.");
        }

        AfterHatchedState = clonedState;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(IsHatched);
        builder.Append(_afterHatchedState is not null);
        if (_afterHatchedState is { } savedState)
        {
            builder.Append(savedState.Id);
        }
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
