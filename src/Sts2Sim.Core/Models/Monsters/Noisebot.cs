namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class Noisebot : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 19, 18);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 24, 23);
    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var noise = new MoveState("NOISE_MOVE", Move, new StatusIntent(2)); noise.FollowUpState = noise;
        return new MonsterMoveStateMachine(new MonsterState[] { noise }, noise);
    }
    private async Task Move(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            var discard = (Dazed)ModelDb.Card<Dazed>().MutableClone();
            discard.AssignOwner(target.Player!);
            await CardPileCmd.Generate(Creature.CombatState!, discard, PileType.Discard, creator: null);
            var draw = (Dazed)ModelDb.Card<Dazed>().MutableClone();
            draw.AssignOwner(target.Player!);
            await CardPileCmd.Generate(Creature.CombatState!, draw, PileType.Draw,
                creator: null, position: CardPilePosition.Random);
        }
    }
    private int Ascension(AscensionLevel level, int high, int low) => Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
