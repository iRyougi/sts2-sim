namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class Axebot : MonsterModel
{
    private int? _stockAmount;
    public int StockAmount
    {
        get => _stockAmount ?? 2;
        set
        {
            AssertMutable();
            _stockAmount = value;
        }
    }
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 76, 70) + (2 - StockAmount) * 10;
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 86, 78) + (2 - StockAmount) * 10;
    private int OneTwoDamage => Ascension(AscensionLevel.DeadlyEnemies, 11, 10);
    private int UppercutDamage => Ascension(AscensionLevel.DeadlyEnemies, 18, 14);
    private int BootBlock => Ascension(AscensionLevel.DeadlyEnemies, 15, 10);
    private int BootStrength => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        if (StockAmount > 0)
        {
            await PowerCmd.Apply<StockPower>(Creature.CombatState!, Creature, StockAmount, null, null);
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var boot = new MoveState("BOOT_UP_MOVE", BootUp, new DefendIntent(), new BuffIntent());
        var oneTwo = new MoveState("ONE_TWO_MOVE", OneTwo, new MultiAttackIntent(OneTwoDamage, 2));
        var uppercut = new MoveState("HAMMER_UPPERCUT_MOVE", Uppercut, new SingleAttackIntent(UppercutDamage), new DebuffIntent());
        boot.FollowUpState = uppercut;
        uppercut.FollowUpState = oneTwo;
        oneTwo.FollowUpState = uppercut;
        return new MonsterMoveStateMachine(new MonsterState[] { boot, oneTwo, uppercut }, _stockAmount.HasValue ? boot : uppercut);
    }

    private async Task BootUp(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, BootBlock, ValueProp.Move, null, null);
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, BootStrength * (2 - StockAmount), Creature, null);
    }
    private Task OneTwo(IReadOnlyList<Creature> targets) => DamageCmd.Attack(OneTwoDamage).WithHitCount(2).FromMonster(this).Execute();
    private async Task Uppercut(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(UppercutDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 2m, Creature, null);
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 2m, Creature, null);
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(_stockAmount.HasValue);
        builder.Append(_stockAmount.GetValueOrDefault());
    }

    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
