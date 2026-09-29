namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class LeafSlimeM : MonsterModel
{
    private const int StickyAmount = 2;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 33, 32);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 36, 35);

    private int ClumpDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 9, 8);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var clumpShot = new MoveState(
            "CLUMP_SHOT",
            ClumpShotMove,
            new SingleAttackIntent(ClumpDamage));
        var stickyShot = new MoveState(
            "STICKY_SHOT",
            StickyShotMove,
            new StatusIntent(StickyAmount));
        stickyShot.FollowUpState = clumpShot;
        clumpShot.FollowUpState = stickyShot;
        return new MonsterMoveStateMachine(new MonsterState[] { clumpShot, stickyShot }, stickyShot);
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
            for (int index = 0; index < StickyAmount; index++)
            {
                var slimed = (Slimed)ModelDb.Card<Slimed>().MutableClone();
                slimed.AssignOwner(target.Player!);
                await CardPileCmd.Generate(combatState, slimed, PileType.Discard, creator: null);
            }
        }
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
