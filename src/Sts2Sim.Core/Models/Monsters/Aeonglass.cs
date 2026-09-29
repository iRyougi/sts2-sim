namespace Sts2Sim.Core.Models.Monsters;

using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.ValueProps;

public sealed class Aeonglass : MonsterModel
{
    public int AdditionalStrength { get; private set; }
    public int WitherUpgradeCount { get; private set; }

    public override int MinInitialHp => Ascension(AscensionLevel.ToughEnemies, 535, 512);

    public override int MaxInitialHp => MinInitialHp;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var ebb = new MoveState(
            "EBB_MOVE", Ebb, new SingleAttackIntent(EbbDamage), new DefendIntent());
        var eyeLasers = new MoveState(
            "EYE_LASERS_MOVE", EyeLasers, new MultiAttackIntent(EyeLaserDamage, 2));
        var increasingIntensity = new MoveState(
            "INCREASING_INTENSITY_MOVE", IncreasingIntensity, new StatusIntent(WitherCount), new BuffIntent());
        ebb.FollowUpState = eyeLasers;
        eyeLasers.FollowUpState = increasingIntensity;
        increasingIntensity.FollowUpState = ebb;
        return new MonsterMoveStateMachine([ebb, eyeLasers, increasingIntensity], ebb);
    }

    public override async Task AfterAddedToRoom()
    {
        await base.AfterAddedToRoom();
        foreach (Creature target in Creature.CombatState!.Allies.Where(target => target.Player is not null))
        {
            WitheringPresencePower presence = await PowerCmd.Apply<WitheringPresencePower>(
                Creature.CombatState, Creature, 6m, Creature, null)
                ?? throw new InvalidOperationException("Aeonglass must gain Withering Presence.");
            presence.Target = target;
        }

        await PowerCmd.Apply<ArtifactPower>(Creature.CombatState, Creature, 3m, Creature, null);
    }

    public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
    {
        if (card is Wither wither)
        {
            ApplyFakeUpgrades(wither, WitherUpgradeCount);
        }

        return Task.CompletedTask;
    }

    private int EbbDamage => Ascension(AscensionLevel.DeadlyEnemies, 26, 22);
    private const int EbbBlock = 33;
    private int EyeLaserDamage => Ascension(AscensionLevel.DeadlyEnemies, 12, 11);
    private int WitherCount => Ascension(AscensionLevel.DeadlyEnemies, 2, 1);
    private int BaseStrength => Ascension(AscensionLevel.DeadlyEnemies, 4, 3);

    private async Task Ebb(IReadOnlyList<Creature> targets)
    {
        await DamageCmd.Attack(EbbDamage).FromMonster(this).Execute();
        await CreatureCmd.GainBlock(Creature.CombatState!, Creature, EbbBlock, ValueProp.Move, null, null);
    }

    private Task EyeLasers(IReadOnlyList<Creature> targets) =>
        DamageCmd.Attack(EyeLaserDamage).WithHitCount(2).FromMonster(this).Execute();

    private async Task IncreasingIntensity(IReadOnlyList<Creature> targets)
    {
        foreach (Wither wither in Creature.CombatState!.Allies
                     .Where(target => target.Player is not null)
                     .SelectMany(target => target.Player!.PlayerCombatState!.AllPiles)
                     .SelectMany(pile => pile.Cards)
                     .OfType<Wither>()
                     .ToArray())
        {
            wither.FakeUpgrade();
        }

        WitherUpgradeCount++;
        foreach (Creature target in targets.Where(target => target.Player is not null))
        {
            for (int index = 0; index < WitherCount; index++)
            {
                var wither = (Wither)ModelDb.Card<Wither>().MutableClone();
                wither.AssignOwner(target.Player!);
                await CardPileCmd.Generate(Creature.CombatState, wither, PileType.Discard, creator: null);
            }
        }

        await PowerCmd.Apply<StrengthPower>(
            Creature.CombatState, Creature, BaseStrength + AdditionalStrength, Creature, null);
        AdditionalStrength++;
    }

    private static void ApplyFakeUpgrades(Wither wither, int count)
    {
        for (int index = 0; index < count; index++)
        {
            wither.FakeUpgrade();
        }
    }

    internal override void AppendCombatStateDescription(
        ref global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionBuilder builder,
        global::Sts2Sim.Core.Combat.StateDescription.CombatStateDescriptionContext context)
    {
        builder.Append(AdditionalStrength);
        builder.Append(WitherUpgradeCount);
    }

    private int Ascension(AscensionLevel level, int high, int low) =>
        Creature?.CombatState?.RunState.Ascension.GetValueIfAscension(level, high, low) ?? low;
}
