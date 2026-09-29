namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SnappingJaxfruit : MonsterModel
{
    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 34, 31);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 36, 33);

    private int EnergyDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 4, 3);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var energyOrb = new MoveState(
            "ENERGY_ORB_MOVE",
            EnergyOrb,
            new SingleAttackIntent(EnergyDamage),
            new BuffIntent());
        energyOrb.FollowUpState = energyOrb;
        return new MonsterMoveStateMachine(new MonsterState[] { energyOrb }, energyOrb);
    }

    private async Task EnergyOrb(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(EnergyDamage).FromMonster(this).Execute();
        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState!,
            Creature,
            2m,
            Creature,
            cardSource: null);
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
