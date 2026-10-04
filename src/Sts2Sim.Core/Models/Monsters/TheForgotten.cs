namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class TheForgotten : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 111, 106);

    public override int MaxInitialHp => MinInitialHp;

    private int Dread => Value(AscensionLevel.DeadlyEnemies, 15, 13) +
                         (int)(Creature.GetPower<DexterityPower>()?.Amount ?? 0);

    public override async Task AfterAddedToRoom()
    {
        await PowerCmd.Apply<PossessSpeedPower>(Creature.CombatState!, Creature, 1m, null, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var miasma = new MoveState("MIASMA", Miasma, new DebuffIntent(), new DefendIntent(), new BuffIntent());
        var dread = new MoveState("DREAD", DreadMove, new SingleAttackIntent(() => Dread));
        miasma.FollowUpState = dread;
        dread.FollowUpState = miasma;
        return new MonsterMoveStateMachine([miasma, dread], miasma);
    }

    private async Task Miasma(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<DexterityPower>(Creature.CombatState!, target, -2m, Creature, null);
        }

        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, 8m, ValueProp.Move, null, null);
        await PowerCmd.Apply<DexterityPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }

    private Task DreadMove(IReadOnlyList<Creature> _) => DamageCmd.Attack(Dread).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int ascensionValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, ascensionValue, normalValue) ?? normalValue;
}
