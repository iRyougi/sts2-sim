namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class PhrogParasite : MonsterModel
{
    private const int InfectionCount = 3;
    private const int InfestedAmount = 4;

    public override int MinInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 66, 61);

    public override int MaxInitialHp => AscensionValue(AscensionLevel.ToughEnemies, 68, 64);

    private int LashDamage => AscensionValue(AscensionLevel.DeadlyEnemies, 5, 4);

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        await PowerCmd.Apply<InfestedPower>(
            Creature.CombatState!,
            Creature,
            InfestedAmount,
            Creature,
            cardSource: null);
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var infect = new MoveState(
            "INFECT_MOVE",
            InfectMove,
            new StatusIntent(InfectionCount));
        var lash = new MoveState(
            "LASH_MOVE",
            LashMove,
            new MultiAttackIntent(LashDamage, 4));
        infect.FollowUpState = lash;
        lash.FollowUpState = infect;
        return new MonsterMoveStateMachine(new MonsterState[] { infect, lash }, infect);
    }

    private async Task InfectMove(IReadOnlyList<Creature> targets)
    {
        ICombatState combatState = Creature.CombatState
            ?? throw new InvalidOperationException("Infect requires active combat.");
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            for (int index = 0; index < InfectionCount; index++)
            {
                var infection = (Infection)ModelDb.Card<Infection>().MutableClone();
                infection.AssignOwner(target.Player!);
                await CardPileCmd.Generate(combatState, infection, PileType.Discard, creator: null);
            }
        }
    }

    private async Task LashMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(LashDamage).WithHitCount(4).FromMonster(this).Execute();
    }

    private int AscensionValue(AscensionLevel level, int ascensionValue, int fallbackValue) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(
            level,
            ascensionValue,
            fallbackValue) ?? fallbackValue;
}
