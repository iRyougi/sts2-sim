namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class LivingFog : MonsterModel
{
    private int _bloatAmount = 1;

    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 82, 80);
    public override int MaxInitialHp => MinInitialHp;
    private int AdvancedGasDamage => Value(AscensionLevel.DeadlyEnemies, 9, 8);
    private int BloatDamage => Value(AscensionLevel.DeadlyEnemies, 6, 5);
    private int SuperGasBlastDamage => Value(AscensionLevel.DeadlyEnemies, 9, 8);
    private int BloatAmount
    {
        get => _bloatAmount;
        set
        {
            AssertMutable();
            _bloatAmount = value;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var advanced = new MoveState("ADVANCED_GAS_MOVE", AdvancedGas, new SingleAttackIntent(() => AdvancedGasDamage), new CardDebuffIntent());
        var bloat = new MoveState("BLOAT_MOVE", Bloat, new SingleAttackIntent(() => BloatDamage), new SummonIntent());
        var superGas = new MoveState("SUPER_GAS_BLAST_MOVE", SuperGas, new SingleAttackIntent(() => SuperGasBlastDamage));
        advanced.FollowUpState = bloat;
        bloat.FollowUpState = superGas;
        superGas.FollowUpState = bloat;
        return new MonsterMoveStateMachine([advanced, superGas, bloat], advanced);
    }

    private async Task AdvancedGas(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(AdvancedGasDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<SmoggyPower>(Creature.CombatState!, target, 1m, Creature, null);
        }
    }

    private async Task Bloat(IReadOnlyList<Creature> _)
    {
        ICombatState combatState = Creature.CombatState!;
        for (int index = 0; index < BloatAmount; index++)
        {
            string? slot = NextBombSlot(combatState);
            if (slot is null)
            {
                break;
            }

            var bomb = (GasBomb)ModelDb.Monster<GasBomb>().MutableClone();
            await CreatureCmd.Add(bomb, combatState, CombatSide.Enemy, slot);
        }

        await DamageCmd.Attack(BloatDamage).FromMonster(this).Execute();
    }

    private Task SuperGas(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(SuperGasBlastDamage).FromMonster(this).Execute();

    private static string? NextBombSlot(ICombatState combatState) =>
        new[] { "bomb1", "bomb2", "bomb3", "bomb4", "bomb5" }
            .FirstOrDefault(slot => combatState.Enemies.All(enemy => enemy.SlotName != slot));

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_bloatAmount);

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
