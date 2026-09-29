namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class TheInsatiable : MonsterModel
{
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 341, 321);
    public override int MaxInitialHp => MinInitialHp;
    private int ThrashDamage => Ascension(AscensionLevel.DeadlyEnemies, 9, 8);
    private int BiteDamage => Ascension(AscensionLevel.DeadlyEnemies, 31, 28);
    private int SalivateStrength => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var liquify = new MoveState("LIQUIFY_GROUND_MOVE", Liquify, new BuffIntent(), new StatusIntent(6));
        var thrash1 = new MoveState("THRASH_MOVE", _ => DamageCmd.Attack(ThrashDamage).WithHitCount(2).FromMonster(this).Execute(), new MultiAttackIntent(ThrashDamage, 2));
        var bite = new MoveState("LUNGING_BITE_MOVE", _ => DamageCmd.Attack(BiteDamage).FromMonster(this).Execute(), new SingleAttackIntent(BiteDamage));
        var salivate = new MoveState("SALIVATE_MOVE", _ => PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, SalivateStrength, Creature, null), new BuffIntent());
        var thrash2 = new MoveState("THRASH_MOVE_2", _ => DamageCmd.Attack(ThrashDamage).WithHitCount(2).FromMonster(this).Execute(), new MultiAttackIntent(ThrashDamage, 2));
        liquify.FollowUpState = thrash1;
        thrash1.FollowUpState = bite;
        bite.FollowUpState = salivate;
        salivate.FollowUpState = thrash2;
        thrash2.FollowUpState = thrash1;
        return new MonsterMoveStateMachine([liquify, thrash1, bite, salivate, thrash2], liquify);
    }

    private async Task Liquify(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            SandpitPower sandpit = await PowerCmd.Apply<SandpitPower>(Creature.CombatState!, Creature, 4m, Creature, null)
                ?? throw new InvalidOperationException("Liquify must create a Sandpit instance.");
            sandpit.Target = target;
            for (int i = 0; i < 6; i++)
            {
                var card = (FranticEscape)ModelDb.Card<FranticEscape>().MutableClone();
                card.AssignOwner(target.Player!);
                await CardPileCmd.Generate(Creature.CombatState!, card, i < 3 ? PileType.Draw : PileType.Discard,
                    creator: null, position: CardPilePosition.Random);
            }
        }
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
