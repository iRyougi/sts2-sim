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

    public MoveState AfterHatchedState { get; private set; } = null!;

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
            SetMoveImmediate(AfterHatchedState, forceTransition: true);
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
        foreach (PowerModel power in Creature.Powers.Where(power => power is not MinionPower).ToArray())
        {
            await PowerCmd.Remove(power);
        }

        await Hatch();
    }

    private async Task Hatch()
    {
        int hp = RunRng.Niche.NextInt(HatchlingMinHp, HatchlingMaxHp + 1);
        Creature.SetMaxHpInternal(hp);
        await CreatureCmd.Heal(Creature, hp);
    }

    private Task Nibble(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(NibbleDamage).FromMonster(this).Execute();

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
