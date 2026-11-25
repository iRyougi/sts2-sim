using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.RestSite;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Byrdpip = Sts2Sim.Core.Models.Relics.Byrdpip;
using WuppyMonster = Sts2Sim.Core.Models.Monsters.Wuppy;
using WuppyRelic = Sts2Sim.Core.Models.Relics.Wuppy;

namespace Sts2Sim.Core.Tests.Models.Relics;

/// <summary>v0.113.1 标准模式宠物（#928，计划 09-S-928 §4）：Byrdpip 进化链与 Wuppy。</summary>
[Collection("ModelDb")]
public sealed class V01131PetTests : IDisposable
{
    public V01131PetTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("evolve-gate")]
    [InlineData("evolve-keeps-upgrade")]
    [InlineData("god-byrd")]
    [InlineData("egg-dedupe")]
    [InlineData("wuppy-carries-hp")]
    [InlineData("wuppy-dead-at-start")]
    [InlineData("restore")]
    [InlineData("event-help")]
    [InlineData("event-cautious")]
    [InlineData("event-ignore")]
    [InlineData("event-gate")]
    [InlineData("clone")]
    public async Task Pet_MatchesNative(string scenario)
    {
        string ctx = $"scenario={scenario}";
        (RunState run, Player player) = CreatePlayer(scenario);
        switch (scenario)
        {
            case "evolve-gate":
            {
                await RelicCmd.Obtain(ModelDb.Relic<Byrdpip>(), player);
                Byrdpip relic = player.Relics.OfType<Byrdpip>().Single();
                relic.SwoopsPlayed = 5;
                Assert.DoesNotContain(RestDecisions(run, player), d => d is EvolveRestSiteOption);
                CombatRoom room = await EnterCombat(run);
                player.PlayerCombatState!.Energy = 99;
                Creature enemy = room.Engine.State.Enemies[0];
                enemy.SetMaxHpInternal(999);
                enemy.SetCurrentHpInternal(999);
                await room.Engine.PlayCardAsync(player, AddToHand<ByrdSwoop>(player), enemy);
                Assert.Equal(6, relic.SwoopsPlayed);
                await room.Engine.PlayCardAsync(player, AddToHand<ByrdSwoop>(player), enemy);
                Assert.Equal(6, relic.SwoopsPlayed);
                Assert.Single(RestDecisions(run, player), d => d is EvolveRestSiteOption);
                return;
            }
            case "evolve-keeps-upgrade":
            {
                await RelicCmd.Obtain(ModelDb.Relic<Byrdpip>(), player);
                AddToDeck<ByrdSwoop>(player);
                CardCmd.Upgrade(AddToDeck<ByrdSwoop>(player));
                int index = player.Relics.ToList().FindIndex(r => r is Byrdpip);
                player.Relics.OfType<Byrdpip>().Single().SwoopsPlayed = 6;
                var room = new RestSiteRoom();
                await room.ResolveAsync(player, room.GetAvailableDecisions(run, player).OfType<EvolveRestSiteOption>().Single());
                Assert.IsType<Byrdine>(player.Relics[index]);
                Assert.DoesNotContain(player.Relics, r => r is Byrdpip);
                Assert.Empty(player.Deck.Cards.OfType<ByrdSwoop>());
                GodByrd[] gods = player.Deck.Cards.OfType<GodByrd>().ToArray();
                Assert.Equal([30m, 40m], gods.Select(g => g.Damage).Order());
                Assert.Equal(1, gods.Count(g => g.IsUpgraded));
                Assert.True(player.HasEventPet(), ctx);
                CombatRoom combat = await EnterCombat(run);
                Assert.IsType<ByrdineMonster>(Assert.Single(player.PlayerCombatState!.Pets).Monster);
                return;
            }
            case "god-byrd":
            {
                await RelicCmd.Obtain(ModelDb.Relic<Byrdine>(), player);
                CombatRoom room = await EnterCombat(run);
                player.PlayerCombatState!.Energy = 99;
                Creature enemy = room.Engine.State.Enemies[0];
                enemy.SetMaxHpInternal(999);
                enemy.SetCurrentHpInternal(999);
                await room.Engine.PlayCardAsync(player, AddToHand<GodByrd>(player), target: null);
                Assert.Equal(30, player.Creature.GetPower<GodByrdPower>()!.Amount);
                Assert.Equal(999, enemy.CurrentHp);
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(969, enemy.CurrentHp);
                Assert.Null(player.Creature.GetPower<GodByrdPower>());
                return;
            }
            case "egg-dedupe":
            {
                AddToDeck<ByrdonisEgg>(player);
                AddToDeck<ByrdonisEgg>(player);
                Assert.Single(RestDecisions(run, player), d => d is RestSiteDecision.Hatch);
                return;
            }
            case "wuppy-carries-hp":
            {
                await ObtainWuppy(player, maxHp: 30);
                CombatRoom room = await EnterCombat(run);
                Creature pet = Assert.Single(player.PlayerCombatState!.Pets);
                Assert.IsType<WuppyMonster>(pet.Monster);
                Assert.Equal((30, 30), (pet.CurrentHp, pet.MaxHp));
                Assert.NotNull(pet.GetPower<WuppysLoyaltyPower>());
                int playerHp = player.Creature.CurrentHp;
                Creature enemy = room.Engine.State.Enemies[0];
                await DamageCmd.Attack(7m).FromMonster(enemy.Monster!).Execute();
                Assert.Equal(playerHp, player.Creature.CurrentHp);
                Assert.Equal(23, pet.CurrentHp);
                WuppyRelic relic = player.Relics.OfType<WuppyRelic>().Single();
                Assert.Equal(23, relic.CurrentHp);
                CombatRoom next = await EnterCombat(run);
                Creature nextPet = Assert.Single(player.PlayerCombatState!.Pets);
                Assert.NotSame(pet, nextPet);
                Assert.Equal((23, 30), (nextPet.CurrentHp, nextPet.MaxHp));
                return;
            }
            case "wuppy-dead-at-start":
            {
                await ObtainWuppy(player, maxHp: 60);
                player.Relics.OfType<WuppyRelic>().Single().CurrentHp = 0;
                AddToDeck<Sts2Sim.Core.Models.Cards.Melancholy>(player);
                CombatRoom room = await EnterCombat(run);
                // v0.114.0 (#1110): a Wuppy that starts the fight dead no longer lowers Melancholy's cost.
                var melancholy = player.PlayerCombatState!.Hand.Cards.Concat(player.PlayerCombatState.DrawPile.Cards)
                    .OfType<Sts2Sim.Core.Models.Cards.Melancholy>().Single();
                Assert.Equal(3, melancholy.EnergyCost);
                Creature pet = Assert.Single(player.PlayerCombatState!.Pets);
                Assert.True(pet.IsDead, ctx);
                Assert.Contains(pet, room.Engine.State.Allies);
                int playerHp = player.Creature.CurrentHp;
                await DamageCmd.Attack(7m).FromMonster(room.Engine.State.Enemies[0].Monster!).Execute();
                Assert.Equal(playerHp - 7, player.Creature.CurrentHp);
                return;
            }
            case "restore":
            {
                await ObtainWuppy(player, maxHp: 60);
                WuppyRelic relic = player.Relics.OfType<WuppyRelic>().Single();
                Assert.False(Assert.Single(RestDecisions(run, player).OfType<RestoreRestSiteOption>()).IsEnabled, ctx);
                relic.CurrentHp = 12;
                var room = new RestSiteRoom();
                RestoreRestSiteOption restore = room.GetAvailableDecisions(run, player).OfType<RestoreRestSiteOption>().Single();
                Assert.True(restore.IsEnabled);
                VisibleOption row = Assert.Single(VisibleDecisionProjection.ForRestSite(player, [restore]).Metadata.DisplayOptions);
                Assert.Contains(VisibleValue.Int("CurrentHp", 12), row.Values);
                await room.ResolveAsync(player, restore);
                Assert.Equal(60, relic.CurrentHp);
                return;
            }
            case "event-help":
            case "event-cautious":
            {
                EventModel ev = await BeginEvent(run, player);
                int hp = player.Creature.CurrentHp;
                int deck = player.Deck.Cards.Count;
                bool help = scenario == "event-help";
                await Choose(ev, help ? "HELP_IMMEDIATELY" : "BE_CAUTIOUS");
                WuppyRelic relic = player.Relics.OfType<WuppyRelic>().Single();
                Assert.Equal(help ? (60, 60) : (30, 30), (relic.MaxHp, relic.CurrentHp));
                Assert.Equal(help ? deck + 1 : deck, player.Deck.Cards.Count);
                Assert.Equal(help ? hp : hp - 3, player.Creature.CurrentHp);
                if (help) Assert.IsType<Injury>(player.Deck.Cards[^1]);
                Assert.True(ev.IsFinished, ctx);
                return;
            }
            case "event-ignore":
            {
                EventModel ev = await BeginEvent(run, player);
                await Choose(ev, "IGNORE");
                Assert.True(ev.TryDequeuePendingRewardOffer(out RewardsSet? rewards), ctx);
                Assert.NotNull(rewards!.Potion);
                Assert.Empty(player.Relics.OfType<WuppyRelic>());
                return;
            }
            case "event-gate":
            {
                var canonical = ModelDb.Event<WuppyInTrouble>();
                Assert.True(canonical.IsAllowed(run), ctx);
                AddToDeck<ByrdonisEgg>(player);
                Assert.False(canonical.IsAllowed(run), ctx);
                Assert.Equal(typeof(WuppyInTrouble), UnderdocksEventPool.All[^1]);
                return;
            }
            default:
            {
                Assert.Equal("clone", scenario);
                await ObtainWuppy(player, maxHp: 60);
                CombatRoom room = await EnterCombat(run);
                Creature sourcePet = Assert.Single(player.PlayerCombatState!.Pets);
                CombatState clone = room.Engine.State.Clone();
                Player clonedPlayer = Assert.Single(clone.Players);
                Creature clonedPet = Assert.Single(clonedPlayer.PlayerCombatState!.Pets);
                WuppyRelic clonedRelic = clonedPlayer.Relics.OfType<WuppyRelic>().Single();
                Assert.Same(clonedPet, clonedRelic.CombatWuppy);
                await DamageCmd.Attack(9m).FromMonster(clone.Enemies[0].Monster!).Execute();
                Assert.Equal(51, clonedRelic.CurrentHp);
                Assert.Equal(60, player.Relics.OfType<WuppyRelic>().Single().CurrentHp);
                Assert.Equal(60, sourcePet.CurrentHp);
                // #974：战后遗物仍持有已离场的 Wuppy 生物；战斗外跑局克隆必须成功，
                // 且克隆世界的下一场战斗与源世界一致。
                await CreatureCmd.Kill(room.Engine.State.Enemies[0]);
                await room.ResolveOutcomeAsync(generateRewards: false);
                await room.Exit(run);
                run.PopCurrentRoom();
                Assert.Null(player.Creature.CombatState);
                foreach ((string label, RunState world) in new[]
                    { ("source", run), ("exact", run.CloneExact()), ("reseeded", run.CloneReseeded(974)) })
                {
                    Player worldPlayer = Assert.Single(world.Players);
                    WuppyRelic worldRelic = worldPlayer.Relics.OfType<WuppyRelic>().Single();
                    CombatRoom next = await EnterCombat(world);
                    Creature nextPet = Assert.Single(worldPlayer.PlayerCombatState!.Pets);
                    Assert.True(ReferenceEquals(nextPet, worldRelic.CombatWuppy), $"{ctx} world={label}");
                    Assert.True((60, 60) == (nextPet.CurrentHp, nextPet.MaxHp), $"{ctx} world={label}");
                    await DamageCmd.Attack(9m).FromMonster(next.Engine.State.Enemies[0].Monster!).Execute();
                    Assert.True(worldRelic.CurrentHp == 51, $"{ctx} world={label} hp={worldRelic.CurrentHp}");
                }
                return;
            }
        }
    }

    private static IReadOnlyList<RestSiteDecision> RestDecisions(RunState run, Player player) =>
        new RestSiteRoom().GetAvailableDecisions(run, player);

    private static async Task ObtainWuppy(Player player, int maxHp)
    {
        var wuppy = (WuppyRelic)ModelDb.Relic<WuppyRelic>().MutableClone();
        wuppy.MaxHp = maxHp;
        await RelicCmd.Obtain(wuppy, player);
    }

    private static async Task<EventModel> BeginEvent(RunState run, Player player)
    {
        player.Creature.SetMaxHpInternal(100);
        player.Creature.SetCurrentHpInternal(60);
        var ev = (EventModel)ModelDb.Event<WuppyInTrouble>().MutableClone();
        var room = new EventRoom(() => ev);
        run.PushRoom(room);
        await room.Enter(run);
        return ev;
    }

    private static Task Choose(EventModel ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));

    private static async Task<CombatRoom> EnterCombat(RunState run)
    {
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        run.PushRoom(room);
        await room.Enter(run);
        return room;
    }

    private static T AddToHand<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static T AddToDeck<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        player.Deck.AddInternal(card, -1);
        return card;
    }

    private static (RunState Run, Player Player) CreatePlayer(string seed)
    {
        var run = new RunState("928-" + seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        return (run, player);
    }
}
