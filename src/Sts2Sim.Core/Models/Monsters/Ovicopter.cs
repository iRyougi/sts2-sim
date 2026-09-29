namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Ovicopter : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 126, 124);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 132, 130);
    private int SmashDamage => Ascension(AscensionLevel.DeadlyEnemies, 17, 16);
    private int TenderizerDamage => Ascension(AscensionLevel.DeadlyEnemies, 8, 7);
    private int PasteStrength => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);
    private bool CanLay => Creature.CombatState!.GetCreaturesOnSide(Creature.Side).Count(creature => creature.IsAlive) <= 3;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var lay = new MoveState("LAY_EGGS_MOVE", LayEggs, new SummonIntent());
        var smash = new MoveState("SMASH_MOVE", Smash, new SingleAttackIntent(SmashDamage));
        var tenderizer = new MoveState("TENDERIZER_MOVE", Tenderizer, new SingleAttackIntent(TenderizerDamage), new DebuffIntent());
        var paste = new MoveState("NUTRITIONAL_PASTE_MOVE", NutritionalPaste, new BuffIntent());
        var branch = new ConditionalBranchState("SUMMON_BRANCH_STATE")
            .AddBranch(_ => CanLay, lay.Id)
            .AddBranch(_ => !CanLay, paste.Id);
        lay.FollowUpState = smash;
        paste.FollowUpState = smash;
        smash.FollowUpState = tenderizer;
        tenderizer.FollowUpState = branch;
        return new MonsterMoveStateMachine(new MonsterState[] { lay, smash, tenderizer, paste, branch }, lay);
    }

    private async Task LayEggs(IReadOnlyList<Creature> targets)
    {
        string[] slots = ["egg5", "egg4", "egg3", "egg2", "egg1"];
        for (int index = 0; index < 3; index++)
        {
            string? slot = slots.FirstOrDefault(candidate =>
                Creature.CombatState!.Enemies.All(enemy => !enemy.IsAlive || enemy.SlotName != candidate));
            if (slot is null)
            {
                return;
            }

            var egg = (ToughEgg)ModelDb.Monster<ToughEgg>().MutableClone();
            Creature created = await CreatureCmd.Add(egg, Creature.CombatState!, Creature.Side, slot);
            await PowerCmd.Apply<MinionPower>(Creature.CombatState!, created, 1m, Creature, null);
        }
    }

    private Task Smash(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(SmashDamage).FromMonster(this).Execute();

    private async Task Tenderizer(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(TenderizerDamage).FromMonster(this).Execute();
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(Creature.CombatState!, target, 2m, Creature, null);
        }
    }

    private Task NutritionalPaste(IReadOnlyList<Creature> targets) =>
        PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, PasteStrength, Creature, null);

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
