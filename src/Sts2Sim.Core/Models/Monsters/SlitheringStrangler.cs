namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class SlitheringStrangler : MonsterModel
{
    private const int ThwackBlock = 5;
    private const int ConstrictAmount = 3;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 54, 53);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 56, 55);

    private int ThwackDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 8, 7);

    private int LashDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 13, 12);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var constrict = new MoveState(
            "CONSTRICT",
            ConstrictMove,
            new DebuffIntent());
        var thwack = new MoveState(
            "THWACK",
            ThwackMove,
            new SingleAttackIntent(ThwackDamage),
            new DefendIntent());
        var lash = new MoveState(
            "LASH",
            LashMove,
            new SingleAttackIntent(LashDamage));
        var random = new RandomBranchState("rand");
        random.AddBranch(thwack, MoveRepeatType.CanRepeatForever, 1f);
        random.AddBranch(lash, MoveRepeatType.CanRepeatForever, 1f);
        constrict.FollowUpState = random;
        thwack.FollowUpState = constrict;
        lash.FollowUpState = constrict;
        return new MonsterMoveStateMachine(
            new MonsterState[] { constrict, thwack, lash, random },
            constrict);
    }

    private async Task ConstrictMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<ConstrictPower>(
                Creature.CombatState!,
                target,
                ConstrictAmount,
                Creature,
                cardSource: null);
        }
    }

    private async Task ThwackMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ThwackDamage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(
            Creature.CombatState!,
            Creature,
            ThwackBlock,
            ValueProp.Move,
            cardSource: null,
            cardPlay: null);
    }

    private async Task LashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(LashDamage).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
