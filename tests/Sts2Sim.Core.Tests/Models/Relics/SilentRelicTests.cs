using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class SilentRelicTests : IDisposable
{
    public SilentRelicTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task HelicalDart_AppliesOneTemporaryDexterityForAnOwnedShivOnly()
    {
        (RunState runState, Player player) = CreateRun("helical-dart");
        await Obtain<HelicalDart>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Shiv shiv = AddToHand<Shiv>(player);

        await room.Engine.PlayCardAsync(player, shiv, Assert.Single(room.Engine.State.HittableEnemies));

        Assert.Equal(1m, Assert.Single(player.Creature.Powers.OfType<HelicalDartPower>()).Amount);
        await room.Engine.EndPlayerTurnAsync();
        Assert.Empty(player.Creature.Powers.OfType<HelicalDartPower>());
    }

    [Fact]
    public async Task NinjaScroll_GeneratesThreeShivsBeforeTheFirstHandDrawOnly()
    {
        (RunState runState, Player player) = CreateRun("ninja-scroll");
        await Obtain<NinjaScroll>(player);
        CombatRoom room = CreateCombatRoom();

        await room.Enter(runState);

        Assert.Equal(3, player.PlayerCombatState!.Hand.Cards.OfType<Shiv>().Count());
        await room.Engine.EndPlayerTurnAsync();
        Assert.Empty(player.PlayerCombatState.Hand.Cards.OfType<Shiv>());
    }

    [Fact]
    public async Task PaperKrane_ReducesDamageFromWeakEnemyOnlyWhenTargetingItsOwner()
    {
        (RunState runState, Player player) = CreateRun("paper-krane");
        await Obtain<PaperKrane>(player);
        Player foreign = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(foreign);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = Assert.Single(room.Engine.State.HittableEnemies);
        await PowerCmd.Apply<WeakPower>(room.Engine.State, enemy, 1m, player.Creature, null);

        decimal ownerDamage = Hook.ModifyDamage(
            room.Engine.State, player.Creature, enemy, 10m, ValueProp.Move, null, null, out _);
        decimal foreignDamage = Hook.ModifyDamage(
            room.Engine.State, foreign.Creature, enemy, 10m, ValueProp.Move, null, null, out _);

        Assert.Equal(6m, ownerDamage);
        Assert.Equal(7.5m, foreignDamage);
    }

    [Fact]
    public async Task SneckoSkull_AddsOneOnlyWhenItsOwnerGivesPoison()
    {
        (RunState runState, Player player) = CreateRun("snecko-skull");
        await Obtain<SneckoSkull>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = Assert.Single(room.Engine.State.HittableEnemies);

        await PowerCmd.Apply<PoisonPower>(room.Engine.State, enemy, 3m, player.Creature, null);

        Assert.Equal(4m, Assert.Single(enemy.Powers.OfType<PoisonPower>()).Amount);
    }

    [Fact]
    public async Task Tingsha_DealsThreeDamageForEachOwnedDiscard()
    {
        (RunState runState, Player player) = CreateRun("tingsha-per-card");
        await Obtain<Tingsha>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = Assert.Single(room.Engine.State.HittableEnemies);
        int hpBefore = enemy.CurrentHp;

        await CardCmd.Discard(new CardModel[] { AddToHand<StrikeSilent>(player), AddToHand<StrikeSilent>(player) });

        Assert.Equal(hpBefore - 6, enemy.CurrentHp);
    }
    [Fact]
    public async Task TingshaAndToughBandages_TriggerForEachOwnedDiscardOnOwnersTurnOnly()
    {
        (RunState runState, Player player) = CreateRun("discard-relics");
        await Obtain<Tingsha>(player);
        await Obtain<ToughBandages>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = Assert.Single(room.Engine.State.HittableEnemies);
        int hpBefore = enemy.CurrentHp;
        StrikeSilent first = AddToHand<StrikeSilent>(player);
        StrikeSilent second = AddToHand<StrikeSilent>(player);

        await CardCmd.Discard(new CardModel[] { first, second });

        Assert.Equal(hpBefore - 6, enemy.CurrentHp);
        Assert.Equal(6m, player.Creature.Block);
        room.Engine.State.CurrentSide = CombatSide.Enemy;
        StrikeSilent duringEnemyTurn = AddToHand<StrikeSilent>(player);
        await CardCmd.Discard(duringEnemyTurn);
        Assert.Equal(hpBefore - 6, enemy.CurrentHp);
        Assert.Equal(6m, player.Creature.Block);
    }

    [Fact]
    public async Task TwistedFunnel_AppliesFourPoisonToEveryHittableEnemyOnlyOnFirstTurn()
    {
        (RunState runState, Player player) = CreateRun("twisted-funnel");
        await Obtain<TwistedFunnel>(player);
        var room = new CombatRoom(() => new MonsterModel[]
        {
            (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(),
        });

        await room.Enter(runState);

        Assert.All(room.Engine.State.HittableEnemies, enemy =>
            Assert.Equal(4m, Assert.Single(enemy.Powers.OfType<PoisonPower>()).Amount));
        await room.Engine.EndPlayerTurnAsync();
        Assert.All(room.Engine.State.HittableEnemies, enemy =>
            Assert.Equal(3m, Assert.Single(enemy.Powers.OfType<PoisonPower>()).Amount));
    }

    private static Task Obtain<TRelic>(Player player)
        where TRelic : RelicModel =>
        RelicCmd.Obtain(ModelDb.Relic<TRelic>(), player);

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        TCard card = CreateOwned<TCard>(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static TCard CreateOwned<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static CombatRoom CreateCombatRoom() =>
        new(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }
}
