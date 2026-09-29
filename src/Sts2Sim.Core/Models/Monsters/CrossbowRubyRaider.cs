namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class CrossbowRubyRaider : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 19, 18);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 22, 21);

    private int FireDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 16, 14);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var fire = new MoveState("FIRE_MOVE", FireMove, new SingleAttackIntent(FireDamage));
        var reload = new MoveState("RELOAD_MOVE", ReloadMove, new DefendIntent());
        fire.FollowUpState = reload;
        reload.FollowUpState = fire;
        return new MonsterMoveStateMachine(new MonsterState[] { reload, fire }, reload);
    }

    private async Task FireMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(FireDamage).FromMonster(this).Execute();
    }

    private async Task ReloadMove(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(
            Creature.CombatState!,
            Creature,
            3m,
            ValueProp.Move,
            cardSource: null,
            cardPlay: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
