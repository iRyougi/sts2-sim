namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TwigSlimeM : MonsterModel
{
    private const int StickyAmount = 1;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 27, 26);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 29, 28);

    private int ClumpDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 12, 11);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var pokeyPounce = new MoveState(
            "POKEY_POUNCE_MOVE",
            ClumpShotMove,
            new SingleAttackIntent(ClumpDamage));
        var stickyShot = new MoveState(
            "STICKY_SHOT_MOVE",
            StickyShotMove,
            new StatusIntent(StickyAmount));
        var random = new RandomBranchState("RAND");
        random.AddBranch(pokeyPounce, maxRepeats: 2);
        random.AddBranch(stickyShot, MoveRepeatType.CannotRepeat, 1f);
        pokeyPounce.FollowUpState = random;
        stickyShot.FollowUpState = random;
        return new MonsterMoveStateMachine(
            new MonsterState[] { pokeyPounce, stickyShot, random },
            stickyShot);
    }

    private async Task ClumpShotMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(ClumpDamage).FromMonster(this).Execute();
    }

    private async Task StickyShotMove(IReadOnlyList<Creature> targets)
    {
        var combatState = Creature.CombatState
            ?? throw new InvalidOperationException("Sticky Shot requires active combat.");
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            var slimed = (Slimed)ModelDb.Card<Slimed>().MutableClone();
            slimed.AssignOwner(target.Player!);
            await CardPileCmd.Generate(combatState, slimed, PileType.Discard, creator: null);
        }
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
