using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Helpers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Tests.Models.Cards;

namespace Sts2Sim.Core.Tests.Models.Events;

/// <summary>v0.113.1 shared events, their items, MyShinies and pool registration (#927, plan 09-S-927 §5).</summary>
[Collection("ModelDb")]
public sealed class V01131NewSharedEventsTests : IDisposable
{
    public V01131NewSharedEventsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
        ModelDb.Inject(typeof(Task11NoDamageMonster));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("Waterfall.BeNice")]
    [InlineData("Waterfall.BeatUp")]
    [InlineData("Waterfall.Negotiate")]
    [InlineData("Waterfall.Locked")]
    [InlineData("Museum.Take")]
    [InlineData("Museum.BreakOut")]
    [InlineData("Shrine.Weapon")]
    [InlineData("Shrine.Shield")]
    [InlineData("Shrine.Wings")]
    [InlineData("Thermae.Baths")]
    [InlineData("Thermae.Oasis")]
    [InlineData("Thermae.Drink")]
    [InlineData("Gates")]
    public async Task NewEvents_GatesBranchesAndVisibleConsequences(string scenario)
    {
        string context = $"scenario={scenario}";
        if (scenario == "Gates")
        {
            AssertGates();
            return;
        }

        string eventName = scenario.Split('.')[0];
        (RunState run, Player player, EventModel ev) = await BeginAsync(eventName switch
        {
            "Waterfall" => typeof(WaterfallOfObviousSecrets),
            "Museum" => typeof(MuseumOfTorture),
            "Shrine" => typeof(ShrineOfTheWarlord),
            _ => typeof(ThermaeNonupeipe),
        }, scenario, gold: scenario == "Waterfall.Locked" ? 69 : 300);
        int deckBefore = player.Deck.Cards.Count;
        int relicsBefore = player.Relics.Count;
        int goldBefore = player.Gold;
        int maxHpBefore = player.Creature.MaxHp;
        int hpBefore = player.Creature.CurrentHp;
        IEnumerable<CardModel> Added() => player.Deck.Cards.Skip(deckBefore);

        switch (scenario)
        {
            case "Waterfall.Locked":
                await Choose(ev, "ENTER");
                Assert.True(ev.CurrentOptions.Single(o => o.Key == "NEGOTIATE_LOCKED").IsLocked, context);
                Assert.DoesNotContain(ev.CurrentOptions, o => o.Key == "NEGOTIATE");
                Assert.Contains(VisibleValue.Int("Gold", 70), Option(ev, "NEGOTIATE_LOCKED").Values);
                return;
            case "Waterfall.BeNice":
                await Choose(ev, "ENTER");
                Assert.Contains(VisibleValue.Id("Card", "DINGY_DAGGER"), Option(ev, "BE_NICE").Values);
                await Choose(ev, "BE_NICE");
                Assert.Equal(0, Assert.IsType<DingyDagger>(Assert.Single(Added())).CurrentUpgradeLevel);
                break;
            case "Waterfall.BeatUp":
                await Choose(ev, "ENTER");
                Assert.DoesNotContain(Option(ev, "BEAT_UP").Values, v => v.Name == "Relic" && v.Kind == VisibleValueKind.ContentId);
                await Choose(ev, "BEAT_UP");
                RelicModel pulled = Assert.Single(player.Relics.Skip(relicsBefore));
                Assert.True(pulled is Circlet || pulled.Rarity is RelicRarity.Common or RelicRarity.Uncommon or RelicRarity.Rare, context);
                Assert.Equal(2, Added().Count(card => card is Guilty));
                break;
            case "Waterfall.Negotiate":
                await Choose(ev, "ENTER");
                await Choose(ev, "NEGOTIATE");
                CardModel rare = Assert.Single(Added());
                Assert.Equal(goldBefore - 70, player.Gold);
                Assert.Equal(CardRarity.Rare, rare.Rarity);
                Assert.Contains(rare.GetType(), player.Character.CardPool.AllCards.Select(card => card.GetType()));
                Assert.False(rare.IsUpgraded, $"{context}: NoUpgradeRoll");
                break;
            case "Museum.Take":
            case "Museum.BreakOut":
                var museum = (MuseumOfTorture)ev;
                await Choose(ev, "CONTINUE");
                Assert.Null(museum.OfferedRelic);
                Assert.DoesNotContain(ev.GetVisibleState().Metadata.DisplayOptions.SelectMany(o => o.Values), v => v.Name == "Relic");
                await Choose(ev, "CONTINUE");
                // Deferred roll (deviation #927): same event-local stream, so the native CalculateVars draw is identical.
                var nativeRng = new Rng(run.Rng.Seed + StringHelper.GetDeterministicHashCode(ev.Id.Entry));
                RelicModel expected = nativeRng.NextItem(new RelicModel[]
                    { ModelDb.Relic<ObsidianVice>(), ModelDb.Relic<Thumbscrew>(), ModelDb.Relic<TormentousCollar>() })!;
                Assert.Same(expected, museum.OfferedRelic);
                Assert.Contains(VisibleValue.Id("Relic", expected.Id.Entry), Option(ev, "TAKE").Values);
                Assert.Contains(VisibleValue.Int("MaxHpLoss", 10), Option(ev, "BREAK_OUT").Values);
                int nonCombat = player.PlayerRng.NonCombatGeneration.Counter;
                if (scenario == "Museum.Take")
                {
                    await Choose(ev, "TAKE");
                    Assert.Equal(expected.GetType(), Assert.Single(player.Relics.Skip(relicsBefore)).GetType());
                    CardModel curse = Assert.Single(Added());
                    Assert.Equal(CardType.Curse, curse.Type);
                    Assert.True(curse.CanBeGeneratedByModifiers, context);
                    Assert.Equal(nonCombat + 1, player.PlayerRng.NonCombatGeneration.Counter);
                }
                else
                {
                    await Choose(ev, "BREAK_OUT");
                    Assert.Equal(maxHpBefore - 10, player.Creature.MaxHp);
                    Assert.Empty(Added());
                    Assert.Equal(nonCombat, player.PlayerRng.NonCombatGeneration.Counter);
                }
                break;
            case "Shrine.Weapon":
                await Choose(ev, "TAKE_WEAPON");
                Assert.IsType<MightyWeapon>(Assert.Single(Added()));
                break;
            case "Shrine.Shield":
                await Choose(ev, "TAKE_SHIELD");
                Assert.IsType<MightyShield>(Assert.Single(Added()));
                break;
            case "Shrine.Wings":
                await Choose(ev, "TAKE_WINGS");
                Assert.IsType<Clumsy>(Assert.Single(Added()));
                Assert.IsType<MightyWings>(Assert.Single(player.Relics.Skip(relicsBefore)));
                break;
            case "Thermae.Baths":
                await Choose(ev, "THERMAL_BATHS");
                Assert.Equal(goldBefore - 88, player.Gold);
                Assert.IsType<BathTowel>(Assert.Single(player.Relics.Skip(relicsBefore)));
                break;
            case "Thermae.Oasis":
                Assert.Contains(VisibleValue.Int("Gold", goldBefore), Option(ev, "PRIVATE_OASIS").Values);
                await Choose(ev, "PRIVATE_OASIS");
                Assert.Equal(0, player.Gold);
                Assert.IsType<BathRobes>(Assert.Single(player.Relics.Skip(relicsBefore)));
                break;
            case "Thermae.Drink":
                var thermae = (ThermaeNonupeipe)ev;
                RelicModel drink = thermae.DrinkRelic;
                Assert.IsNotType<LoomingFruit>(drink);
                Assert.Contains(drink.GetType(), ((Nonupeipe)ModelDb.Get(typeof(Nonupeipe))).AllPossibleOptions.Select(r => r.GetType()));
                Assert.Contains(VisibleValue.Id("Relic", drink.Id.Entry), Option(ev, "DRINK_WATERS").Values);
                player.Creature.GainBlockInternal(50m);
                await Choose(ev, "DRINK_WATERS");
                Assert.Equal(hpBefore - 31, player.Creature.CurrentHp);
                Assert.Equal(3, Added().Count(card => card is Guilty));
                Assert.Equal(drink.GetType(), Assert.Single(player.Relics.Skip(relicsBefore)).GetType());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null);
        }

        Assert.True(ev.IsFinished, context);
    }

    [Theory]
    [InlineData("MyShinies", 0, 0, 0)]
    [InlineData("MyShinies", 0, 19, 0)]
    [InlineData("MyShinies", 0, 20, 1)]
    [InlineData("MyShinies", 0, 39, 1)]
    [InlineData("MyShinies", 0, 40, 2)]
    [InlineData("MyShinies", 1, 9, 0)]
    [InlineData("MyShinies", 1, 10, 1)]
    [InlineData("MyShinies", 1, 39, 3)]
    [InlineData("DingyDagger", 0, 0, 7)]
    [InlineData("DingyDagger", 1, 0, 11)]
    [InlineData("DingyDagger", 2, 0, 16)]
    [InlineData("DingyDagger", 3, 0, 22)]
    [InlineData("DingyDagger", 4, 0, 29)]
    [InlineData("MightyWeapon", 0, 0, 25)]
    [InlineData("MightyWeapon", 1, 0, 30)]
    [InlineData("MightyShield", 0, 0, 20)]
    [InlineData("MightyShield", 1, 0, 25)]
    public async Task NewCards_FaceValuesAndPlay(string cardName, int upgrades, int gold, int expected)
    {
        string context = $"card={cardName}; upgrades={upgrades}; gold={gold}";
        (Player player, CombatRoom room) = await CombatAsync($"927-card-{cardName}-{upgrades}-{gold}", RoomType.Monster);
        player.Gold = gold;
        CardModel card = cardName switch
        {
            "MyShinies" => Task11CombatTestSupport.AddToHand<MyShinies>(player),
            "DingyDagger" => Task11CombatTestSupport.AddToHand<DingyDagger>(player),
            "MightyWeapon" => Task11CombatTestSupport.AddToHand<MightyWeapon>(player),
            _ => Task11CombatTestSupport.AddToHand<MightyShield>(player),
        };
        for (int i = 0; i < upgrades; i++) card.Upgrade();
        Creature enemy = room.Engine.State.Enemies[0];
        int enemyHp = enemy.CurrentHp;
        int drawBefore = player.PlayerCombatState!.DrawPile.Cards.Count(c => c is Dazed);

        await card.PlayAsync(cardName == "MightyShield" ? null : enemy);

        if (cardName == "MightyShield")
        {
            Assert.Equal(expected, player.Creature.Block);
            Assert.Equal(expected, ((ICardChoiceBaseValueProvider)card).CardChoiceBaseValues!.Value.Block);
        }
        else
        {
            Assert.True(expected == enemyHp - enemy.CurrentHp, context);
            if (cardName != "MyShinies")
                Assert.Equal(expected, ((ICardChoiceBaseValueProvider)card).CardChoiceBaseValues!.Value.Damage);
        }
        int dazed = cardName switch { "MightyWeapon" => 2, "MightyShield" => 3, _ => 0 };
        Assert.Equal(drawBefore + dazed, player.PlayerCombatState.DrawPile.Cards.Count(c => c is Dazed));
        Assert.Equal(cardName == "MyShinies", card.IsColorless);
        if (cardName == "DingyDagger" && upgrades > 0)
        {
            Assert.True(card.IsUpgradable, context);
            card.Downgrade();
            Assert.Equal(0, card.CurrentUpgradeLevel);
            Assert.Equal(7, ((ICardChoiceBaseValueProvider)card).CardChoiceBaseValues!.Value.Damage);
        }
    }

    [Theory]
    [InlineData("BathTowel")]
    [InlineData("BathRobes")]
    [InlineData("MightyWings")]
    [InlineData("ObsidianVice")]
    [InlineData("Thumbscrew")]
    [InlineData("TormentousCollar")]
    public async Task NewRelics_Trigger(string relicName)
    {
        switch (relicName)
        {
            case "BathTowel":
            {
                (Player player, CombatRoom room) = await CombatAsync("927-towel", RoomType.Monster,
                    p => p.Creature.SetCurrentHpInternal(40), typeof(BathTowel));
                Assert.Equal(48, player.Creature.CurrentHp);
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
                Assert.Equal(48, player.Creature.CurrentHp);
                break;
            }
            case "BathRobes":
            {
                (Player monsterPlayer, _) = await CombatAsync("927-robes-monster", RoomType.Monster,
                    p => p.Creature.SetCurrentHpInternal(30), typeof(BathRobes));
                Assert.Equal(30, monsterPlayer.Creature.CurrentHp);
                Assert.False(monsterPlayer.Relics.OfType<BathRobes>().Single().WasUsed);
                (Player bossPlayer, _) = await CombatAsync("927-robes-boss", RoomType.Boss,
                    p => p.Creature.SetCurrentHpInternal(30), typeof(BathRobes));
                BathRobes robes = bossPlayer.Relics.OfType<BathRobes>().Single();
                Assert.Equal(bossPlayer.Creature.MaxHp, bossPlayer.Creature.CurrentHp);
                Assert.True(robes.WasUsed && robes.IsUsedUp);
                bossPlayer.Creature.SetCurrentHpInternal(30);
                await robes.BeforeCombatStart();
                Assert.Equal(30, bossPlayer.Creature.CurrentHp);
                break;
            }
            case "MightyWings":
            {
                (Player player, CombatRoom room) = await CombatAsync("927-wings", RoomType.Monster, null, typeof(MightyWings));
                Creature enemy = room.Engine.State.Enemies[0];
                // TotalDamage excludes overkill, so keep enough HP for both hits to land in full.
                enemy.SetMaxHpInternal(200);
                enemy.SetCurrentHpInternal(200);
                await Task11CombatTestSupport.AddToHand<MightyWeapon>(player).PlayAsync(enemy);
                Assert.Null(player.Creature.GetPower<StrengthPower>());
                MightyWeapon upgraded = Task11CombatTestSupport.AddToHand<MightyWeapon>(player);
                upgraded.Upgrade();
                await upgraded.PlayAsync(enemy);
                Assert.Equal(2m, player.Creature.GetPower<StrengthPower>()!.Amount);
                break;
            }
            case "ObsidianVice":
            {
                (Player player, CombatRoom room) = await CombatAsync("927-vice", RoomType.Monster, null, typeof(ObsidianVice));
                Creature enemy = room.Engine.State.Enemies[0];
                int hand = player.PlayerCombatState!.Hand.Cards.Count;
                Assert.True(hand > 0);
                int hp = enemy.CurrentHp;
                int targets = room.Engine.State.RunState.Rng.CombatTargets.Counter;
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(hp - hand, enemy.CurrentHp);
                Assert.Equal(targets + 1, room.Engine.State.RunState.Rng.CombatTargets.Counter);
                foreach (CardModel card in player.PlayerCombatState.Hand.Cards.ToArray())
                    player.PlayerCombatState.Hand.RemoveInternal(card);
                hp = enemy.CurrentHp;
                targets = room.Engine.State.RunState.Rng.CombatTargets.Counter;
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(hp, enemy.CurrentHp);
                Assert.Equal(targets, room.Engine.State.RunState.Rng.CombatTargets.Counter);
                break;
            }
            case "Thumbscrew":
            {
                (Player player, CombatRoom room) = await CombatAsync("927-thumbscrew", RoomType.Monster, null, typeof(Thumbscrew));
                Creature enemy = room.Engine.State.Enemies[0];
                int hp = enemy.CurrentHp;
                await Task11CombatTestSupport.AddToHand<Peck>(player).PlayAsync(enemy);
                Assert.Equal(8, hp - enemy.CurrentHp);
                hp = enemy.CurrentHp;
                await Task11CombatTestSupport.AddToHand<DingyDagger>(player).PlayAsync(enemy);
                Assert.Equal(7, hp - enemy.CurrentHp);
                break;
            }
            default:
            {
                (_, CombatRoom monster) = await CombatAsync("927-collar-monster", RoomType.Monster, null, typeof(TormentousCollar));
                Assert.Null(monster.Engine.State.Enemies[0].GetPower<WeakPower>());
                (Player player, CombatRoom elite) = await CombatAsync("927-collar-elite", RoomType.Elite, null, typeof(TormentousCollar));
                Creature enemy = elite.Engine.State.Enemies[0];
                Assert.Equal(2m, enemy.GetPower<WeakPower>()!.Amount);
                player.PlayerCombatState!.TurnNumber = 2;
                await player.Relics.OfType<TormentousCollar>().Single()
                    .BeforeSideTurnStart(CombatSide.Player, [player.Creature]);
                Assert.Equal(2m, enemy.GetPower<WeakPower>()!.Amount);
                break;
            }
        }
    }

    [Fact]
    public void EventPools_MatchV01131Order()
    {
        // Shared and Glory orders are pinned by SharedEventIntegrationTests / GloryEventTests.
        // v0.113.1 Underdocks.AllEvents (12 entries; WuppyInTrouble appended by #928).
        Assert.Equal(new[]
        {
            typeof(AbyssalBaths), typeof(DrowningBeacon), typeof(EndlessConveyor), typeof(PunchOff),
            typeof(SpiralingWhirlpool), typeof(SunkenStatue), typeof(SunkenTreasury), typeof(DoorsOfLightAndDark),
            typeof(TrashHeap), typeof(WaterfallOfObviousSecrets), typeof(WaterloggedScriptorium),
            typeof(WuppyInTrouble),
        }, UnderdocksEventPool.All);

        List<Type> colorless = ColorlessCardPool.Instance.AllCards.Select(card => card.GetType()).ToList();
        Assert.Equal(66, colorless.Count);
        int shinies = colorless.IndexOf(typeof(MyShinies));
        Assert.Equal(typeof(MindBlast), colorless[shinies - 1]);
        Assert.Equal(typeof(Nostalgia), colorless[shinies + 1]);
        Assert.All(new CardModel[] { ModelDb.Card<DingyDagger>(), ModelDb.Card<MightyShield>(), ModelDb.Card<MightyWeapon>() },
            card => Assert.True(CardPoolFilters.IsVisuallyColorless(card) && card.Rarity == CardRarity.Event, card.Id.Entry));
        Assert.All(new RelicModel[]
        {
            ModelDb.Relic<BathRobes>(), ModelDb.Relic<BathTowel>(), ModelDb.Relic<MightyWings>(),
            ModelDb.Relic<ObsidianVice>(), ModelDb.Relic<Thumbscrew>(), ModelDb.Relic<TormentousCollar>(),
        }, relic => Assert.Equal(RelicRarity.Event, relic.Rarity));
    }

    private static void AssertGates()
    {
        var museum = ModelDb.Event<MuseumOfTorture>();
        var shrine = ModelDb.Event<ShrineOfTheWarlord>();
        var thermae = ModelDb.Event<ThermaeNonupeipe>();
        var run = new RunState("927-gates", ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        Assert.False(museum.IsAllowed(run), "act index 0");
        run.AdvanceToNextAct();
        Assert.True(museum.IsAllowed(run), "act index 1");
        run.AdvanceToNextAct();
        Assert.False(museum.IsAllowed(run), "act index 2");

        player.Creature.SetMaxHpInternal(100);
        foreach ((int gold, int hp, bool money) in new[] { (175, 75, false), (176, 75, true), (176, 76, false) })
        {
            player.Gold = gold;
            player.Creature.SetCurrentHpInternal(hp);
            bool expected = money && hp <= 75 && run.CurrentAncientEventType != typeof(Nonupeipe);
            Assert.True(expected == thermae.IsAllowed(run), $"thermae gold={gold} hp={hp} ancient={run.CurrentAncientEventType.Name}");
        }

        var seenAncients = new HashSet<bool>();
        for (int i = 0; i < 64 && seenAncients.Count < 2; i++)
        {
            var act3 = new RunState($"927-shrine-{i}", ascensionLevel: 10);
            act3.AddPlayer(Player.CreateForNewRun(ModelDb.Character<Silent>(), act3));
            act3.AdvanceToNextAct();
            act3.AdvanceToNextAct();
            bool isTanx = act3.CurrentAncientEventType == typeof(Tanx);
            seenAncients.Add(isTanx);
            Assert.True(shrine.IsAllowed(act3) == !isTanx, $"shrine seed={i} ancient={act3.CurrentAncientEventType.Name}");
        }
        Assert.Equal(2, seenAncients.Count);
    }

    private static async Task<(RunState Run, Player Player, EventModel Event)> BeginAsync(Type eventType, string seed, int gold)
    {
        var run = new RunState("927-" + seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        player.Gold = gold;
        player.Creature.SetMaxHpInternal(100);
        player.Creature.SetCurrentHpInternal(60);
        var ev = (EventModel)ModelDb.Get(eventType).MutableClone();
        var room = new EventRoom(() => ev);
        run.PushRoom(room);
        await room.Enter(run);
        return (run, player, ev);
    }

    private static async Task<(Player Player, CombatRoom Room)> CombatAsync(
        string seed, RoomType roomType, Action<Player>? configure = null, Type? relic = null)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        configure?.Invoke(player);
        if (relic is not null) await Sts2Sim.Core.Commands.RelicCmd.Obtain((RelicModel)ModelDb.Get(relic), player);
        var room = new CombatRoom(() => (Task11NoDamageMonster)ModelDb.Monster<Task11NoDamageMonster>().MutableClone(), roomType);
        run.PushRoom(room);
        await room.Enter(run);
        return (player, room);
    }

    private static VisibleOption Option(EventModel ev, string key) =>
        Assert.Single(ev.GetVisibleState().Metadata.DisplayOptions, option => option.OptionKey == key);

    private static Task Choose(EventModel ev, string key) =>
        ev.ChooseOption(ev.CurrentOptions.Single(option => option.Key == key));
}
