namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Random;

public sealed class CorpseSlug : MonsterModel
{
    private bool _isRavenous;
    private int _starterMoveIdx;

    public bool IsRavenous
    {
        get => _isRavenous;
        set
        {
            AssertMutable();
            _isRavenous = value;
        }
    }

    public int StarterMoveIdx
    {
        get => _starterMoveIdx;
        set
        {
            AssertMutable();
            _starterMoveIdx = value;
        }
    }

    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 27, 25);

    public override int MaxInitialHp => Value(AscensionLevel.ToughEnemies, 29, 27);

    private int WhipSlapDamage => 3;
    private int GlompDamage => Value(AscensionLevel.DeadlyEnemies, 9, 8);
    private int RavenousAmount => Value(AscensionLevel.DeadlyEnemies, 5, 4);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<RavenousPower>(Creature.CombatState!, Creature, RavenousAmount, Creature, null);
    }

    public static void EnsureCorpseSlugsStartWithDifferentMoves(IEnumerable<MonsterModel> monsters, Rng rng)
    {
        int starter = rng.NextInt(3);
        foreach (CorpseSlug slug in monsters.OfType<CorpseSlug>())
        {
            slug.StarterMoveIdx = starter % 3;
            starter++;
        }
    }

    internal void StartRavenousStun()
    {
        string nextMoveId = MoveStateMachine?.StateLog.LastOrDefault()?.Id ?? "WHIP_SLAP_MOVE";
        SetMoveImmediate(CreateRavenousStunnedMove(nextMoveId), forceTransition: true);
    }

    private MoveState CreateRavenousStunnedMove(string nextMoveId) =>
        new("STUNNED", RavenousStunned, new StunIntent())
        {
            FollowUpStateId = nextMoveId,
            MustPerformOnceBeforeTransitioning = true,
            CombatCloneFactory = clonedMonster => ((CorpseSlug)clonedMonster).CreateRavenousStunnedMove(nextMoveId),
        };

    private Task RavenousStunned(IReadOnlyList<Creature> _)
    {
        IsRavenous = false;
        return Task.CompletedTask;
    }
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var whip = new MoveState("WHIP_SLAP_MOVE", Whip, new MultiAttackIntent(WhipSlapDamage, 2));
        var glomp = new MoveState("GLOMP_MOVE", Glomp, new SingleAttackIntent(() => GlompDamage));
        var goop = new MoveState("GOOP_MOVE", Goop, new DebuffIntent());
        whip.FollowUpState = glomp;
        glomp.FollowUpState = goop;
        goop.FollowUpState = whip;
        MonsterState initial = (StarterMoveIdx % 3) switch
        {
            0 => whip,
            1 => glomp,
            _ => goop,
        };
        return new MonsterMoveStateMachine([whip, glomp, goop], initial);
    }

    private Task Whip(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(WhipSlapDamage).WithHitCount(2).FromMonster(this).Execute();

    private Task Glomp(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(GlompDamage).FromMonster(this).Execute();

    private async Task Goop(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 2m, Creature, null);
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(IsRavenous);
        builder.Append(StarterMoveIdx);
    }
    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
