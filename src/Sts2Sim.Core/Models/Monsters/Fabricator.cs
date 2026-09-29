namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Fabricator : MonsterModel
{
    private Type? _lastSpawned;
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 155, 150);
    public override int MaxInitialHp => MinInitialHp;
    private int StrikeDamage => Ascension(AscensionLevel.DeadlyEnemies, 21, 18);
    private int DisintegrateDamage => Ascension(AscensionLevel.DeadlyEnemies, 13, 11);
    private bool CanFabricate => Creature.CombatState!.Enemies.Count(enemy => enemy.IsAlive) < 4;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var fabricate = new MoveState("FABRICATE_MOVE", Fabricate, new SummonIntent());
        var strike = new MoveState("FABRICATING_STRIKE_MOVE", FabricatingStrike, new SingleAttackIntent(StrikeDamage), new SummonIntent());
        var disintegrate = new MoveState("DISINTEGRATE_MOVE", Disintegrate, new SingleAttackIntent(DisintegrateDamage));
        var random = new RandomBranchState("RAND");
        random.AddBranch(fabricate, MoveRepeatType.CanRepeatForever);
        random.AddBranch(strike, MoveRepeatType.CanRepeatForever);
        var branch = new ConditionalBranchState("fabricateBranch")
            .AddBranch(_ => CanFabricate, random.Id)
            .AddBranch(_ => !CanFabricate, disintegrate.Id);
        fabricate.FollowUpState = branch;
        strike.FollowUpState = branch;
        disintegrate.FollowUpState = branch;
        return new MonsterMoveStateMachine(new MonsterState[] { fabricate, strike, disintegrate, random, branch }, branch);
    }
    private async Task Fabricate(IReadOnlyList<Creature> targets)
    {
        await Spawn(typeof(Guardbot), typeof(Noisebot));
        await Spawn(typeof(Zapbot), typeof(Stabbot));
    }
    private async Task FabricatingStrike(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(StrikeDamage).FromMonster(this).Execute();
        await Spawn(typeof(Zapbot), typeof(Stabbot));
    }
    private Task Disintegrate(IReadOnlyList<Creature> targets) => DamageCmd.Attack(DisintegrateDamage).FromMonster(this).Execute();
    private async Task Spawn(params Type[] choices)
    {
        ICombatState combatState = Creature.CombatState!;
        if (!combatState.IsLiveCombat()) return;
        int livingTeammates = combatState.Enemies.Count(
            enemy => enemy.IsAlive && !ReferenceEquals(enemy, Creature));
        string? slotName = GetNextSlot();
        if (livingTeammates >= 4 || slotName is null) return;
        Type choice = choices.Where(type => type != _lastSpawned).ToArray() is Type[] available && available.Length > 0
            ? available[combatState.RunState.Rng.MonsterAi.NextInt(available.Length)]
            : choices[0];
        _lastSpawned = choice;
        var bot = (MonsterModel)ModelDb.Get(choice).MutableClone();
        Creature spawned = await CreatureCmd.Add(bot, combatState, CombatSide.Enemy, slotName);
        await PowerCmd.Apply<MinionPower>(combatState, spawned, 1m, Creature, null);
    }
    private string? GetNextSlot() => Enumerable.Range(1, 4).Select(index => $"bot{index}")
        .FirstOrDefault(slot => Creature.CombatState!.Enemies.All(enemy => !enemy.IsAlive || enemy.SlotName != slot));

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_lastSpawned?.FullName);

    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
