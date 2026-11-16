using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Combat;

[Collection("ModelDb")]
public sealed class PotionOwnerDeathWindowTests : IDisposable
{
    public PotionOwnerDeathWindowTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(PotionOwnerDeathRelic)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("foul-live")]
    [InlineData("foul-dead")]
    [InlineData("foul-fairy")]
    [InlineData("final-enemy")]
    public async Task OwnerDeathWindows(string scenario)
    {
        var run = new RunState("potion-owner-death-" + scenario, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        var observer = new PotionOwnerDeathObserver();
        room.ConfigureObserver(observer);
        await room.Enter(run);

        var relic = (PotionOwnerDeathRelic)ModelDb.Relic<PotionOwnerDeathRelic>().MutableClone();
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
        PlayerCombatState effectState = player.PlayerCombatState!;
        foreach (CardModel card in effectState.Hand.Cards.ToArray())
            CardPileCmd.Remove(card);
        Assert.Empty(effectState.Hand.Cards);

        bool finalEnemy = scenario == "final-enemy";
        Creature enemy = Assert.Single(room.Engine.State.Enemies);
        if (finalEnemy)
            enemy.SetCurrentHpInternal(20);
        else
            Assert.True(enemy.CurrentHp > 12);
        int enemyHpBefore = enemy.CurrentHp;
        player.Creature.SetCurrentHpInternal(scenario == "foul-live" ? 13 : 12);
        PotionModel outer = player.AddPotionInternal(finalEnemy
            ? ModelDb.Potion<FirePotion>()
            : ModelDb.Potion<FoulPotion>());
        PotionModel? fairy = scenario == "foul-fairy"
            ? player.AddPotionInternal(ModelDb.Potion<FairyInABottle>())
            : null;
        Assert.False(room.Engine.IsOverOrEnding);

        await PotionCmd.Use(outer, player, finalEnemy ? enemy : null);

        Assert.DoesNotContain(outer, player.PotionSlots);
        Assert.Same(player, outer.Owner);
        Assert.Equal(finalEnemy ? 0 : enemyHpBefore - 12, enemy.CurrentHp);
        int expectedHp = scenario switch
        {
            "foul-live" => 1,
            "foul-dead" => 0,
            "foul-fairy" => Math.Max(1, (int)(player.Creature.MaxHp * 0.30m)),
            _ => 12,
        };
        Assert.Equal(expectedHp, player.Creature.CurrentHp);
        Assert.Equal(scenario == "foul-dead", player.Creature.IsDead);
        Assert.Equal(scenario is "foul-dead" or "final-enemy", room.Engine.IsOverOrEnding);
        Assert.Equal(0, effectState.CardOrPotionEffectDepth);
        Assert.Equal(0, observer.Aborted);
        Assert.Equal(0, observer.ActiveScopes);
        Assert.True(observer.PairingMatches);
        Assert.Equal(fairy is null ? 2 : 4, observer.Events.Count);
        Assert.Same(outer, observer.Events[0].Potion);
        Assert.Same(outer, observer.Events[^1].Potion);
        Assert.Equal((true, 0), (observer.Events[0].Started, observer.Events[0].EffectDepth));
        Assert.Equal((false, 0), (observer.Events[^1].Started, observer.Events[^1].EffectDepth));
        if (fairy is not null)
        {
            Assert.DoesNotContain(fairy, player.PotionSlots);
            Assert.Same(fairy, observer.Events[1].Potion);
            Assert.Same(fairy, observer.Events[2].Potion);
            Assert.Equal((true, 1), (observer.Events[1].Started, observer.Events[1].EffectDepth));
            Assert.Equal((false, 1), (observer.Events[2].Started, observer.Events[2].EffectDepth));
            Assert.Equal(1, relic.AfterUses.Count(potion => ReferenceEquals(potion, fairy)));
        }
        // Nested Fairy finishes while Foul still owns effect depth 1. The outer boundary
        // alone may report an empty hand, and the existing ending gate suppresses victory.
        Assert.Equal(scenario is "foul-live" or "foul-fairy" ? 1 : 0, relic.EmptyHands);
        int expectedAfterUses = scenario == "foul-dead" ? 0 : 1;
        int actualAfterUses = relic.AfterUses.Count(potion => ReferenceEquals(potion, outer));
        Assert.True(actualAfterUses == expectedAfterUses,
            $"owner-death boundary: {scenario} emitted AfterPotionUsed for the outer potion; " +
            $"expected {expectedAfterUses}, observed {actualAfterUses}.");
    }
}

file sealed class PotionOwnerDeathRelic : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;
    public List<PotionModel> AfterUses { get; } = [];
    public int EmptyHands { get; private set; }

    public override Task AfterPotionUsed(PotionModel potion, Player player)
    {
        AfterUses.Add(potion);
        return Task.CompletedTask;
    }

    public override Task AfterHandEmptied(Player player)
    {
        EmptyHands++;
        return Task.CompletedTask;
    }
}

file sealed class PotionOwnerDeathObserver : ICombatObserver
{
    private readonly Stack<PotionModel> _active = new();
    public List<(PotionModel Potion, bool Started, int EffectDepth)> Events { get; } = [];
    public int ActiveScopes => _active.Count;
    public int Aborted { get; private set; }
    public bool PairingMatches { get; private set; } = true;

    public void PotionUseStarted(PotionModel potion, Creature? target)
    {
        _active.Push(potion);
        Events.Add((potion, true, potion.Owner.PlayerCombatState!.CardOrPotionEffectDepth));
    }

    public void PotionUseFinished(PotionModel potion, Creature? target)
    {
        PairingMatches &= _active.TryPop(out PotionModel? started) && ReferenceEquals(started, potion);
        Events.Add((potion, false, potion.Owner.PlayerCombatState!.CardOrPotionEffectDepth));
    }

    public void PotionUseAborted(PotionModel potion, Creature? target) => Aborted++;
    public void CombatStarted(CombatState state) { }
    public void PlayerTurnStarted(CombatState state) { }
    public void CardDrawn(CardModel card) { }
    public void CardPlayStarted(CardModel card, Creature? target) { }
    public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay) { }
    public void EnemyMoveStarted(Creature source, string moveId) { }
    public void EnemyMoveFinished(Creature source, string moveId) { }
    public void PlayerTurnEnded(CombatState state) { }
}
