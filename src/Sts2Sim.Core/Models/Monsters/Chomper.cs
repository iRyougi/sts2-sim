namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Chomper : MonsterModel
{
    public bool ScreamFirst { get; set; }
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 63, 60);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 67, 64);
    private int ClampDamage => Ascension(AscensionLevel.DeadlyEnemies, 9, 8);

    public override async Task AfterAddedToRoom()
    {
        await PowerCmd.Apply<ArtifactPower>(Creature.CombatState!, Creature, 2m, Creature, null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var clamp = new MoveState("CLAMP_MOVE", _ => DamageCmd.Attack(ClampDamage).WithHitCount(2).FromMonster(this).Execute(),
            new MultiAttackIntent(ClampDamage, 2));
        var screech = new MoveState("SCREECH_MOVE", Screech, new StatusIntent(3));
        clamp.FollowUpState = screech;
        screech.FollowUpState = clamp;
        return new MonsterMoveStateMachine([clamp, screech], ScreamFirst ? screech : clamp);
    }

    private async Task Screech(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            for (int index = 0; index < 3; index++)
            {
                var dazed = (Dazed)ModelDb.Card<Dazed>().MutableClone();
                dazed.AssignOwner(target.Player!);
                await CardPileCmd.Generate(Creature.CombatState!, dazed, PileType.Discard, creator: null);
            }
        }
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
