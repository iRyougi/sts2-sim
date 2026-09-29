namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Exoskeleton : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 26, 24);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 30, 28);
    private int SkitterRepeats => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);
    private int MandiblesDamage => Ascension(AscensionLevel.DeadlyEnemies, 9, 8);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<HardToKillPower>(Creature.CombatState!, Creature, 9m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var skitter = new MoveState("SKITTER_MOVE", Skitter, new MultiAttackIntent(1, SkitterRepeats));
        var mandibles = new MoveState("MANDIBLES_MOVE", Mandibles, new SingleAttackIntent(MandiblesDamage));
        var enrage = new MoveState("ENRAGE_MOVE", Enrage, new BuffIntent());
        var random = new RandomBranchState("RAND");
        random.AddBranch(skitter, MoveRepeatType.CannotRepeat);
        random.AddBranch(mandibles, MoveRepeatType.CannotRepeat);
        var initial = new ConditionalBranchState("INIT_MOVE")
            .AddBranch(owner => owner.SlotName == "first", skitter.Id)
            .AddBranch(owner => owner.SlotName == "second", mandibles.Id)
            .AddBranch(owner => owner.SlotName == "third", enrage.Id)
            .AddBranch(_ => true, random.Id);
        skitter.FollowUpState = random;
        mandibles.FollowUpState = enrage;
        enrage.FollowUpState = random;
        return new MonsterMoveStateMachine(
            new MonsterState[] { initial, random, skitter, mandibles, enrage },
            initial);
    }

    private Task Skitter(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(1).WithHitCount(SkitterRepeats).FromMonster(this).Execute();

    private Task Mandibles(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(MandiblesDamage).FromMonster(this).Execute();

    private Task Enrage(IReadOnlyList<Creature> targets) =>
        PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
