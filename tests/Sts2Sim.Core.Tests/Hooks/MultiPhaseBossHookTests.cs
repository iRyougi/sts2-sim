using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Hooks;

/// <summary>
/// 多阶段 Boss（TestSubject）靠"死了但不移出战斗、Power 也不摘"来实现复活。
/// 前两个 hook 的默认值必须是"照常移除"，只有显式覆写才留下；
/// 第三个是生成牌通知，默认无副作用。
/// </summary>
[Collection("ModelDb")]
public sealed class MultiPhaseBossHookTests : IDisposable
{
    public MultiPhaseBossHookTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
        [
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight), typeof(WanderingGrunt), typeof(Wither), typeof(DefaultRemovalPower),
            typeof(SurvivingRemovalPower), typeof(GenerationProbePower), typeof(MutatingGenerationPower),
        ]);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void ShouldPowerBeRemovedAfterOwnerDeath_DefaultsToTrue()
    {
        Assert.True(new PlainPower().ShouldPowerBeRemovedAfterOwnerDeath());
    }

    [Fact]
    public void ShouldPowerBeRemovedAfterOwnerDeath_CanBeOverriddenToFalse()
    {
        Assert.False(new SurvivingPower().ShouldPowerBeRemovedAfterOwnerDeath());
    }

    [Fact]
    public void ShouldDisappearFromDoom_DefaultsToTrue()
    {
        Assert.True(new PlainModel().ShouldDisappearFromDoom());
    }

    [Fact]
    public void ShouldDisappearFromDoom_CanBeOverriddenToFalse()
    {
        Assert.False(new PersistentModel().ShouldDisappearFromDoom());
    }

    [Fact]
    public async Task AfterCardGeneratedForCombat_DefaultsToNoOp()
    {
        await new PlainPower().AfterCardGeneratedForCombat(
            ModelDb.Card<Wither>(),
            creator: null);
    }

    [Fact]
    public async Task Generate_OverloadsDispatchCombatGenerationWithCorrectCreator()
    {
        (Player player, Player alternateCreator, CombatRoom room) =
            await CreateTwoPlayerCombatAsync("boss-hook-generation-creators");
        GenerationProbePower probe = (await PowerCmd.Apply<GenerationProbePower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: null,
            cardSource: null))!;
        Wither defaultCreatorCard = CreateOwned<Wither>(player);
        Wither nullCreatorCard = CreateOwned<Wither>(player);

        await CardPileCmd.Generate(room.Engine.State, defaultCreatorCard, PileType.Hand);
        await CardPileCmd.Generate(room.Engine.State, nullCreatorCard, PileType.Discard, creator: alternateCreator);

        Assert.Collection(
            probe.Calls,
            call =>
            {
                Assert.Same(defaultCreatorCard, call.Card);
                Assert.Same(player, call.Creator);
            },
            call =>
            {
                Assert.Same(nullCreatorCard, call.Card);
                Assert.Same(alternateCreator, call.Creator);
            });
    }

    [Fact]
    public async Task EnterCombat_DoesNotDispatchCombatGenerationHook()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("boss-hook-entry-is-not-generation");
        GenerationProbePower probe = (await PowerCmd.Apply<GenerationProbePower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: null,
            cardSource: null))!;

        await CardPileCmd.EnterCombat(room.Engine.State, CreateOwned<Wither>(player), PileType.Hand);

        Assert.Empty(probe.Calls);
    }

    [Fact]
    public async Task Generate_SnapshotsListenersWhenGenerationHookRemovesAnotherPower()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("boss-hook-generation-listener-mutation");
        await PowerCmd.Apply<MutatingGenerationPower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: null,
            cardSource: null);
        GenerationProbePower probe = (await PowerCmd.Apply<GenerationProbePower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: null,
            cardSource: null))!;
        Wither generated = CreateOwned<Wither>(player);

        await CardPileCmd.Generate(room.Engine.State, generated, PileType.Hand);

        Assert.Single(probe.Calls);
        Assert.DoesNotContain(player.Creature.Powers, power => ReferenceEquals(power, probe));
    }

    [Fact]
    public async Task OwnerDeath_RemovesDefaultPowerButRetainsSurvivingPower()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("boss-hook-owner-death-power-removal");
        DefaultRemovalPower defaultPower = (await PowerCmd.Apply<DefaultRemovalPower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: null,
            cardSource: null))!;
        SurvivingRemovalPower survivingPower = (await PowerCmd.Apply<SurvivingRemovalPower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: null,
            cardSource: null))!;

        await CreatureCmd.Damage(
            room.Engine.State,
            [player.Creature],
            player.Creature.CurrentHp,
            ValueProp.Move,
            dealer: null,
            cardSource: null,
            cardPlay: null);

        Assert.DoesNotContain(player.Creature.Powers, power => ReferenceEquals(power, defaultPower));
        Assert.Contains(player.Creature.Powers, power => ReferenceEquals(power, survivingPower));
    }

    private static TCard CreateOwned<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }

    private static async Task<(Player Player, Player AlternateCreator, CombatRoom Room)> CreateTwoPlayerCombatAsync(
        string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player alternateCreator = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        runState.AddPlayer(alternateCreator);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, alternateCreator, room);
    }

    private sealed class PlainPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;
    }

    private sealed class SurvivingPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
    }

    private sealed class PlainModel : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;
    }

    private sealed class PersistentModel : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override bool ShouldDisappearFromDoom() => false;
    }

    private sealed class GenerationProbePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public List<(CardModel Card, Player? Creator)> Calls { get; } = new();

        public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
        {
            Calls.Add((card, creator));
            return Task.CompletedTask;
        }
    }

    private sealed class MutatingGenerationPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override Task AfterCardGeneratedForCombat(CardModel card, Player? creator)
        {
            GenerationProbePower probe = Owner.Powers.OfType<GenerationProbePower>().Single();
            return PowerCmd.Remove(probe);
        }
    }

    private sealed class DefaultRemovalPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;
    }

    private sealed class SurvivingRemovalPower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public override bool ShouldPowerBeRemovedAfterOwnerDeath() => false;
    }
}
