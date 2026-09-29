namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class SlimedBerserker : MonsterModel
{
    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 281, 261);
    public override int MaxInitialHp => MinInitialHp;

    private int Pummel => Value(AscensionLevel.DeadlyEnemies, 5, 4);
    private int Smother => Value(AscensionLevel.DeadlyEnemies, 33, 30);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var vomit = new MoveState("VOMIT_ICHOR_MOVE", Vomit, new StatusIntent(10));
        var pummel = new MoveState(
            "FURIOUS_PUMMELING_MOVE",
            Pummeling,
            new MultiAttackIntent(Pummel, 4));
        var hug = new MoveState(
            "LEECHING_HUG_MOVE",
            Hug,
            new DebuffIntent(),
            new BuffIntent());
        var smother = new MoveState("SMOTHER_MOVE", SmotherMove, new SingleAttackIntent(Smother));

        vomit.FollowUpState = pummel;
        pummel.FollowUpState = hug;
        hug.FollowUpState = smother;
        smother.FollowUpState = vomit;

        return new MonsterMoveStateMachine([vomit, pummel, hug, smother], vomit);
    }

    private async Task Vomit(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            for (int i = 0; i < 10; i++)
            {
                var card = (Slimed)ModelDb.Card<Slimed>().MutableClone();
                card.AssignOwner(target.Player!);
                await CardPileCmd.Generate(
                    Creature.CombatState!,
                    card,
                    PileType.Discard,
                    creator: null);
            }
        }
    }

    private Task Pummeling(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(Pummel).WithHitCount(4).FromMonster(this).Execute();

    private async Task Hug(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<WeakPower>(Creature.CombatState!, target, 3m, null, null);
        }

        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, 3m, Creature, null);
    }

    private Task SmotherMove(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(Smother).FromMonster(this).Execute();

    private int Value(AscensionLevel level, int ascendedValue, int normalValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascendedValue,
            normalValue) ?? normalValue;
}
