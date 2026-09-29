namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Combat.StateDescription;

public sealed class SoulFysh : MonsterModel
{
    private bool _isInvisible;

    public override int MinInitialHp => Value(AscensionLevel.ToughEnemies, 221, 211);

    public override int MaxInitialHp => MinInitialHp;

    private int DeGasDamage => Value(AscensionLevel.DeadlyEnemies, 18, 16);

    private int ScreamDamage => Value(AscensionLevel.DeadlyEnemies, 15, 13);

    private int GazeDamage => Value(AscensionLevel.DeadlyEnemies, 8, 7);

    private const int BeckonMoveAmount = 2;
    private const int GazeMoveAmount = 1;
    private const int ScreamMoveAmount = 3;

    public bool IsInvisible
    {
        get => _isInvisible;
        set
        {
            AssertMutable();
            _isInvisible = value;
        }
    }

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var beckon = new MoveState("BECKON_MOVE", BeckonMove, new StatusIntent(BeckonMoveAmount));
        var deGas = new MoveState("DE_GAS_MOVE", DeGasMove, new SingleAttackIntent(() => DeGasDamage));
        var gaze = new MoveState(
            "GAZE_MOVE",
            GazeMove,
            new SingleAttackIntent(() => GazeDamage),
            new StatusIntent(GazeMoveAmount));
        var fade = new MoveState("FADE_MOVE", FadeMove, new BuffIntent());
        var scream = new MoveState(
            "SCREAM_MOVE",
            ScreamMove,
            new SingleAttackIntent(() => ScreamDamage),
            new DebuffIntent());

        beckon.FollowUpState = deGas;
        deGas.FollowUpState = gaze;
        gaze.FollowUpState = fade;
        fade.FollowUpState = scream;
        scream.FollowUpState = beckon;

        return new MonsterMoveStateMachine([beckon, deGas, gaze, scream, fade], beckon);
    }

    private async Task BeckonMove(IReadOnlyList<Creature> targets)
    {
        foreach (Creature target in targets)
        {
            Player player = target.Player
                ?? throw new InvalidOperationException("SoulFysh Beckon requires a player target.");
            ICombatState combatState = Creature.CombatState
                ?? throw new InvalidOperationException("SoulFysh Beckon requires active combat.");

            Beckon drawBeckon = (Beckon)ModelDb.Card<Beckon>().MutableClone();
            drawBeckon.AssignOwner(player);
            await CardPileCmd.Generate(
                combatState,
                drawBeckon,
                PileType.Draw,
                creator: null,
                position: CardPilePosition.Random);

            Beckon discardBeckon = (Beckon)ModelDb.Card<Beckon>().MutableClone();
            discardBeckon.AssignOwner(player);
            await CardPileCmd.Generate(
                combatState,
                discardBeckon,
                PileType.Discard,
                creator: null);
        }
    }

    private async Task GazeMove(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(GazeDamage).FromMonster(this).Execute();

        foreach (Creature target in targets)
        {
            Player player = target.Player
                ?? throw new InvalidOperationException("SoulFysh Gaze requires a player target.");
            Beckon beckon = (Beckon)ModelDb.Card<Beckon>().MutableClone();
            beckon.AssignOwner(player);
            await CardPileCmd.Generate(
                Creature.CombatState
                    ?? throw new InvalidOperationException("SoulFysh Gaze requires active combat."),
                beckon,
                PileType.Discard,
                creator: null);
        }
    }

    private Task DeGasMove(IReadOnlyList<Creature> _) =>
        DamageCmd.Attack(DeGasDamage).FromMonster(this).Execute();

    private async Task ScreamMove(IReadOnlyList<Creature> targets)
    {
        IsInvisible = false;
        await DamageCmd.Attack(ScreamDamage).FromMonster(this).Execute();

        foreach (Creature target in targets)
        {
            await PowerCmd.Apply<VulnerablePower>(
                Creature.CombatState!,
                target,
                ScreamMoveAmount,
                Creature,
                null);
        }
    }

    private async Task FadeMove(IReadOnlyList<Creature> _)
    {
        IsInvisible = true;
        await PowerCmd.Apply<IntangiblePower>(
            Creature.CombatState!,
            Creature,
            2m,
            Creature,
            null);
    }

    internal override void AppendCombatStateDescription(
        ref CombatStateDescriptionBuilder builder,
        CombatStateDescriptionContext context) =>
        builder.Append(_isInvisible);

    private int Value(AscensionLevel level, int high, int normal) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, normal) ?? normal;
}
