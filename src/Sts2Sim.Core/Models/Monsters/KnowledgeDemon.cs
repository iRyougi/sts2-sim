namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;

public sealed class KnowledgeDemon : MonsterModel
{
    private int _curseCounter;
    public interface IChoosable { Task OnChosen(); }
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 399, 379);
    public override int MaxInitialHp => MinInitialHp;
    private int SlapDamage => Ascension(AscensionLevel.DeadlyEnemies, 18, 17);
    private int OverwhelmingDamage => Ascension(AscensionLevel.DeadlyEnemies, 9, 8);
    private int PonderDamage => Ascension(AscensionLevel.DeadlyEnemies, 13, 11);
    private int PonderStrength => Ascension(AscensionLevel.DeadlyEnemies, 3, 2);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var curse = new MoveState("CURSE_OF_KNOWLEDGE_MOVE", Curse, new DebuffIntent());
        var slap = new MoveState("SLAP_MOVE", _ => DamageCmd.Attack(SlapDamage).FromMonster(this).Execute(), new SingleAttackIntent(SlapDamage));
        var overwhelming = new MoveState("KNOWLEDGE_OVERWHELMING_MOVE", _ => DamageCmd.Attack(OverwhelmingDamage).WithHitCount(3).FromMonster(this).Execute(), new MultiAttackIntent(OverwhelmingDamage, 3));
        var ponder = new MoveState("PONDER_MOVE", Ponder, new SingleAttackIntent(PonderDamage), new HealIntent(), new BuffIntent());
        var branch = new ConditionalBranchState("POST_PONDER")
            .AddBranch(_ => _curseCounter < 3, curse.Id)
            .AddBranch(_ => _curseCounter >= 3, slap.Id);
        curse.FollowUpState = slap;
        slap.FollowUpState = overwhelming;
        overwhelming.FollowUpState = ponder;
        ponder.FollowUpState = branch;
        return new MonsterMoveStateMachine([curse, slap, overwhelming, ponder, branch], curse);
    }

    private async Task Curse(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            CardModel disintegration = MutableCard<Disintegration>(target);
            ((Disintegration)disintegration).PowerAmount = 6 + _curseCounter;
            CardModel alternative = _curseCounter switch
            {
                0 => MutableCard<MindRot>(target),
                1 => MutableCard<Sloth>(target),
                _ => MutableCard<WasteAway>(target),
            };
            IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
                Creature.CombatState!, target.Player!, [disintegration, alternative], 1, 1, this);
            await ((IChoosable)selected[0]).OnChosen();
        }
        _curseCounter++;
    }

    private async Task Ponder(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(PonderDamage).FromMonster(this).Execute();
        await CreatureCmd.Heal(Creature, 30m * targets.Count);
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, PonderStrength, Creature, null);
    }

    private static T MutableCard<T>(Creature target) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(target.Player!);
        return card;
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context) =>
        builder.Append(_curseCounter);

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
