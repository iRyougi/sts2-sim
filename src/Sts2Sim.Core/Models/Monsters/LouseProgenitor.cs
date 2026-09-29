namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class LouseProgenitor : MonsterModel
{
    public bool Curled { get; set; }
    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 138, 134);
    public override int MaxInitialHp => Ascension(AscensionLevel.ToughEnemies, 141, 136);
    private int WebDamage => Ascension(AscensionLevel.DeadlyEnemies, 10, 9);
    private int PounceDamage => Ascension(AscensionLevel.DeadlyEnemies, 16, 14);
    private int CurlBlock => Ascension(AscensionLevel.ToughEnemies, 18, 14);

    public override async Task AfterAddedToRoom() =>
        await PowerCmd.Apply<CurlUpPower>(Creature.CombatState!, Creature, CurlBlock, Creature, null);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var web = new MoveState("WEB_CANNON_MOVE", Web, new SingleAttackIntent(WebDamage), new DebuffIntent());
        var curl = new MoveState("CURL_AND_GROW_MOVE", Curl, new DefendIntent(), new BuffIntent());
        var pounce = new MoveState("POUNCE_MOVE", _ => DamageCmd.Attack(PounceDamage).FromMonster(this).Execute(), new SingleAttackIntent(PounceDamage));
        web.FollowUpState = curl;
        curl.FollowUpState = pounce;
        pounce.FollowUpState = web;
        return new MonsterMoveStateMachine([web, curl, pounce], web);
    }

    private async Task Web(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(WebDamage).FromMonster(this).Execute();
        foreach (Creature target in targets) await PowerCmd.Apply<FrailPower>(Creature.CombatState!, target, 2m, Creature, null);
    }

    private async Task Curl(IReadOnlyList<Creature> targets)
    {
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, CurlBlock, ValueProp.Move, null, null);
        await PowerCmd.Apply<StrengthPower>(Creature.CombatState!, Creature, Ascension(AscensionLevel.DeadlyEnemies, 7, 5), Creature, null);
        Curled = true;
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
