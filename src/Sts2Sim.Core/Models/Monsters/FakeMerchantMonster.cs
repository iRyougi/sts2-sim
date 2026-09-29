using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

namespace Sts2Sim.Core.Models.Monsters;

public sealed class FakeMerchantMonster : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 175, 165);
    public override int MaxInitialHp => MinInitialHp;
    private int SwipeDamage => Ascension(AscensionLevel.DeadlyEnemies, 15, 13);
    private int ThrowDamage => Ascension(AscensionLevel.DeadlyEnemies, 10, 9);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var swipe = new MoveState("SWIPE_MOVE", Swipe, new SingleAttackIntent(SwipeDamage));
        var spew = new MoveState("SPEW_COINS_MOVE", Spew, new MultiAttackIntent(2, 8));
        var throwRelic = new MoveState("THROW_RELIC_MOVE", ThrowRelic, new SingleAttackIntent(ThrowDamage), new DebuffIntent());
        var enrage = new MoveState("ENRAGE_MOVE", Enrage, new BuffIntent());
        var random = new RandomBranchState("RAND_MOVE");
        random.AddBranch(swipe, MoveRepeatType.CannotRepeat);
        random.AddBranch(spew, MoveRepeatType.CannotRepeat);
        random.AddBranch(throwRelic, MoveRepeatType.CannotRepeat);
        random.AddBranch(enrage, cooldown: 3, MoveRepeatType.CannotRepeat);
        var randomAttack = new RandomBranchState("RAND_ATTACK_MOVE");
        randomAttack.AddBranch(swipe, MoveRepeatType.CannotRepeat);
        randomAttack.AddBranch(spew, MoveRepeatType.CannotRepeat);
        randomAttack.AddBranch(throwRelic, MoveRepeatType.CannotRepeat);
        swipe.FollowUpState = random;
        spew.FollowUpState = random;
        enrage.FollowUpState = random;
        throwRelic.FollowUpState = randomAttack;
        return new MonsterMoveStateMachine([swipe, spew, throwRelic, enrage, random, randomAttack], swipe);
    }
    private Task Swipe(IReadOnlyList<Creature> targets) => DamageCmd.Attack(SwipeDamage).FromMonster(this).Execute();
    private Task Spew(IReadOnlyList<Creature> targets) => DamageCmd.Attack(2m).FromMonster(this).WithHitCount(8).Execute();
    private async Task ThrowRelic(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ThrowDamage).FromMonster(this).Execute();
        foreach (var target in targets) await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 1m, Creature, null);
    }
    private Task Enrage(IReadOnlyList<Creature> targets) =>
        PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
