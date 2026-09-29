namespace Sts2Sim.Core.Tests.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class ArtifactPowerTests : IDisposable
{
    public ArtifactPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(ArtifactPower),
            typeof(WeakPower),
            typeof(StrengthPower),
            typeof(TrainingDummy),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ApplyDebuff_ThroughPowerCmd_BlocksEachApplicationAndConsumesOneArtifactLayer()
    {
        (CombatState combatState, Creature target, Creature applier) = CreateCombat("artifact-debuff");
        ArtifactPower artifact = (await PowerCmd.Apply<ArtifactPower>(
            combatState,
            target,
            2m,
            applier,
            cardSource: null))!;

        WeakPower? first = await PowerCmd.Apply<WeakPower>(
            combatState,
            target,
            3m,
            applier,
            cardSource: null);

        Assert.Null(first);
        Assert.DoesNotContain(target.Powers, power => power is WeakPower);
        Assert.Equal(1, artifact.Amount);
        Assert.Contains(artifact, target.Powers);

        WeakPower? second = await PowerCmd.Apply<WeakPower>(
            combatState,
            target,
            3m,
            applier,
            cardSource: null);

        Assert.Null(second);
        Assert.DoesNotContain(target.Powers, power => power is WeakPower);
        Assert.DoesNotContain(artifact, target.Powers);

        WeakPower? unblocked = await PowerCmd.Apply<WeakPower>(
            combatState,
            target,
            3m,
            applier,
            cardSource: null);

        Assert.NotNull(unblocked);
        Assert.Equal(3, unblocked!.Amount);
        Assert.Contains(unblocked, target.Powers);
    }

    [Fact]
    public async Task ApplyBuff_ThroughPowerCmd_DoesNotConsumeArtifactOrBlockBuff()
    {
        (CombatState combatState, Creature target, Creature applier) = CreateCombat("artifact-buff");
        ArtifactPower artifact = (await PowerCmd.Apply<ArtifactPower>(
            combatState,
            target,
            1m,
            applier,
            cardSource: null))!;

        StrengthPower? strength = await PowerCmd.Apply<StrengthPower>(
            combatState,
            target,
            2m,
            applier,
            cardSource: null);

        Assert.NotNull(strength);
        Assert.Equal(2, strength!.Amount);
        Assert.Equal(1, artifact.Amount);
        Assert.Contains(artifact, target.Powers);
    }

    [Fact]
    public async Task ApplyNegativeCounterBuff_ThroughPowerCmd_IsClassifiedAsDebuffAndBlocked()
    {
        (CombatState combatState, Creature target, Creature applier) = CreateCombat("artifact-negative-counter");
        ArtifactPower artifact = (await PowerCmd.Apply<ArtifactPower>(
            combatState,
            target,
            1m,
            applier,
            cardSource: null))!;

        StrengthPower? strength = await PowerCmd.Apply<StrengthPower>(
            combatState,
            target,
            -2m,
            applier,
            cardSource: null);

        Assert.Null(strength);
        Assert.DoesNotContain(target.Powers, power => power is StrengthPower);
        Assert.DoesNotContain(artifact, target.Powers);
    }

    [Fact]
    public async Task ApplyZeroAmountDebuff_ThroughPowerCmd_DoesNotConsumeArtifactOrInstallPower()
    {
        (CombatState combatState, Creature target, Creature applier) = CreateCombat("artifact-zero");
        ArtifactPower artifact = (await PowerCmd.Apply<ArtifactPower>(
            combatState,
            target,
            2m,
            applier,
            cardSource: null))!;

        WeakPower? weak = await PowerCmd.Apply<WeakPower>(
            combatState,
            target,
            0m,
            applier,
            cardSource: null);

        Assert.Null(weak);
        Assert.Equal(2, artifact.Amount);
        Assert.Contains(artifact, target.Powers);
        Assert.DoesNotContain(target.Powers, power => power is WeakPower);
    }

    private static (CombatState CombatState, Creature Target, Creature Applier) CreateCombat(string seed)
    {
        var runState = new RunState(seed, new Sts2Sim.Core.Content.Acts.Overgrowth());
        var combatState = new CombatState(runState);
        Creature target = combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        Creature applier = combatState.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Player);
        return (combatState, target, applier);
    }
}
