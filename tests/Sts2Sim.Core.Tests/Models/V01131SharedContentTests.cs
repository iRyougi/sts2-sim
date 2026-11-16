using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Odds;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Tests.Models.Cards;
using CombatStateImpl = Sts2Sim.Core.Combat.CombatState;

namespace Sts2Sim.Core.Tests.Models;

/// <summary>#924：v0.113.1 共享及跨色内容的行为差异（计划 09-S-924 §2）。每行在 v0.111 实现下都会失败。</summary>
[Collection("ModelDb")]
public sealed class V01131SharedContentTests : IDisposable
{
    public V01131SharedContentTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("ExpectAFight")]
    [InlineData("ForgottenRitual")]
    [InlineData("HelixDrill")]
    [InlineData("MinionDiveBomb")]
    [InlineData("Omnislice")]
    public async Task Cards_FollowV01131Source(string name)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"924-card-{name}", enemies: 2);
        var state = (CombatStateImpl)room.Engine.State;
        Creature first = state.HittableEnemies[0];
        Creature second = state.HittableEnemies[1];
        PlayerCombatState pcs = player.PlayerCombatState!;
        pcs.Energy = 10;
        switch (name)
        {
            case "ExpectAFight":
            {
                await PowerCmd.Apply<StrengthPower>(state, player.Creature, 2m, player.Creature, null);
                ExpectAFight card = Task11CombatTestSupport.AddToHand<ExpectAFight>(player);
                StrikeRegent strike = Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
                Skewer skewer = Task11CombatTestSupport.AddToHand<Skewer>(player);
                int strikeCost = strike.EnergyCost;
                await room.Engine.PlayCardAsync(player, card, null);
                Assert.Equal(15, player.Creature.Block);
                Assert.Equal(strikeCost - 1, strike.EnergyCost);
                Assert.True(skewer.CostsXEnergy);
                Assert.Equal(new CardChoiceBaseValues(Block: 15, Energy: 1), card.CardChoiceBaseValues);
                CardCmd.Upgrade(card);
                Assert.Equal(new CardChoiceBaseValues(Block: 20, Energy: 1), card.CardChoiceBaseValues);
                break;
            }
            case "ForgottenRitual":
            {
                ForgottenRitual noExhaust = Task11CombatTestSupport.AddToHand<ForgottenRitual>(player);
                await room.Engine.PlayCardAsync(player, noExhaust, null);
                Assert.Equal(9, pcs.Energy);
                StrikeRegent fodder = Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
                await CardPileCmd.Exhaust(state, fodder);
                ForgottenRitual afterExhaust = Task11CombatTestSupport.AddToHand<ForgottenRitual>(player);
                await room.Engine.PlayCardAsync(player, afterExhaust, null);
                Assert.Equal(11, pcs.Energy);
                break;
            }
            case "HelixDrill":
            {
                for (int i = 0; i < 2; i++)
                {
                    DefendRegent defend = Task11CombatTestSupport.AddToHand<DefendRegent>(player);
                    defend.SetTemporaryCostOverrideThisCombat(1);
                    await room.Engine.PlayCardAsync(player, defend, null);
                }
                HelixDrill drill = Task11CombatTestSupport.AddToHand<HelixDrill>(player);
                drill.SetTemporaryCostOverrideThisCombat(2);
                int hpBefore = first.CurrentHp + first.Block;
                // 自动打出不花能量：v0.113.1 扣 EnergySpent=0，得 2 次；旧实现扣当前费用 2 得 0 次。
                await AutoPlayCmd.FromCards(state, player, [drill], first);
                Assert.Equal(hpBefore - 6, first.CurrentHp + first.Block);
                break;
            }
            case "MinionDiveBomb":
            {
                MinionDiveBomb bomb = Task11CombatTestSupport.AddToHand<MinionDiveBomb>(player);
                Assert.True(bomb.TryGetThrashDamageVariable(out decimal baseDamage));
                CardCmd.Upgrade(bomb);
                Assert.True(bomb.TryGetThrashDamageVariable(out decimal upgraded));
                Assert.Equal((11m, 13m), (baseDamage, upgraded));
                break;
            }
            case "Omnislice":
            {
                await PowerCmd.Apply<EnvenomPower>(state, player.Creature, 1m, player.Creature, null);
                Omnislice slice = Task11CombatTestSupport.AddToHand<Omnislice>(player);
                await room.Engine.PlayCardAsync(player, slice, first);
                // 第二段改为 powered Move：攻击触发类能力（Envenom）对溅射目标生效。
                Assert.NotNull(second.GetPower<PoisonPower>());
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }

    [Theory]
    [InlineData("OneForAllPower")]
    [InlineData("DrawCardsNextTurnPower")]
    [InlineData("ImitationLearningPower")]
    [InlineData("RingingTriangle")]
    [InlineData("JuzuBracelet")]
    public async Task PowersAndRelics_FollowV01131Source(string name)
    {
        switch (name)
        {
            case "OneForAllPower":
            {
                (Player player, CombatRoom room) = await CreateCombatAsync("924-one-for-all", enemies: 1);
                await PowerCmd.Apply<OneForAllPower>(room.Engine.State, player.Creature, 5m, player.Creature, null);
                OneForAllPower power = player.Creature.GetPower<OneForAllPower>()!;
                StrikeRegent strike = Task11CombatTestSupport.AddToHand<StrikeRegent>(player);
                decimal Bonus(int spent, int value, bool auto) => power.ModifyDamageAdditive(
                    null, 6m, ValueProps.ValueProp.Move, player.Creature, strike, Play(strike, player, spent, value, auto));
                Assert.Equal(0m, Bonus(spent: 0, value: 1, auto: true));
                Assert.Equal(5m, Bonus(spent: 0, value: 0, auto: true));
                Assert.Equal(5m, Bonus(spent: 0, value: 1, auto: false));
                Assert.Equal(0m, Bonus(spent: 1, value: 1, auto: false));
                break;
            }
            case "DrawCardsNextTurnPower":
            {
                (Player player, CombatRoom room) = await CreateCombatAsync("924-draw-next", enemies: 1);
                var state = room.Engine.State;
                await PowerCmd.Apply<DrawCardsNextTurnPower>(state, player.Creature, 2m, player.Creature, null);
                DrawCardsNextTurnPower power = player.Creature.GetPower<DrawCardsNextTurnPower>()!;
                await Hook.AfterSideTurnStart(state, Sts2Sim.Core.Combat.CombatSide.Player, [player.Creature]);
                Assert.Same(power, player.Creature.GetPower<DrawCardsNextTurnPower>());
                await Hook.AfterModifyingHandDraw(state, [power]);
                Assert.Null(player.Creature.GetPower<DrawCardsNextTurnPower>());
                break;
            }
            case "ImitationLearningPower":
            {
                (Player player, CombatRoom room) = await CreateCombatAsync("924-imitation", enemies: 1);
                var state = room.Engine.State;
                await RelicCmd.Obtain(ModelDb.Relic<VelvetChoker>(), player);
                await PowerCmd.Apply<ImitationLearningPower>(state, player.Creature, 2m, player.Creature, null);
                ImitationLearningPower power = player.Creature.GetPower<ImitationLearningPower>()!;
                power.PlayerTarget = player;
                player.PlayerCombatState!.Energy = 10;
                for (int i = 0; i < 5; i++)
                {
                    DefendRegent defend = Task11CombatTestSupport.AddToHand<DefendRegent>(player);
                    defend.SetTemporaryCostOverrideThisCombat(0);
                    await room.Engine.PlayCardAsync(player, defend, null);
                }
                // 第 6 张能力牌开始后 VelvetChoker 禁止克隆打出：v0.113.1 不扣层数。
                Accuracy accuracy = Task11CombatTestSupport.AddToHand<Accuracy>(player);
                accuracy.SetTemporaryCostOverrideThisCombat(0);
                await room.Engine.PlayCardAsync(player, accuracy, null);
                Assert.Equal(2, power.Amount);
                break;
            }
            case "RingingTriangle":
            {
                var run = new RunState("924-ringing-triangle", new Overgrowth());
                Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
                run.AddPlayer(player);
                await RelicCmd.Obtain(ModelDb.Relic<RingingTriangle>(), player);
                var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
                await room.Enter(run);
                Assert.Equal(1, player.Creature.GetPower<RetainHandPower>()?.Amount);
                Assert.True(player.Relics.OfType<RingingTriangle>().Single().ShouldFlush(player));
                break;
            }
            case "JuzuBracelet":
            {
                var rules = new UnknownMapPointPublicRules(
                    default, default, PreviousPointHadShop: false, AllChildrenAreShops: false,
                    HasJuzuBracelet: true, GoldenPathForCurrentAct: false, HasActThreeLanternKey: false);
                Assert.False(rules.Allows(RoomType.Monster));
                Assert.False(rules.Allows(RoomType.Elite));
                Assert.True(rules.Allows(RoomType.Event));
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, null);
        }
    }

    [Fact]
    public async Task SpoilsMap_ReattachesQuestWhenItEntersDeckInSpoilsAct()
    {
        var run = new RunState("924-spoils-map", [new Overgrowth(), new Hive()]);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var spoils = (SpoilsMap)ModelDb.Card<SpoilsMap>().MutableClone();
        spoils.AssignOwner(player);
        await CardPileCmd.AddToDeck(spoils);
        run.AdvanceToNextAct();
        MapCoord coord = Assert.IsType<MapCoord>(spoils.SpoilsCoord);
        MapPoint point = run.Map.GetPoint(coord)!;
        Assert.Equal(MapPointType.Treasure, point.PointType);
        Assert.Contains(spoils, point.Quests);

        point.RemoveQuest(spoils);
        await Hook.AfterCardChangedPiles(run, spoils, PileType.None, null);
        Assert.Single(point.Quests, quest => ReferenceEquals(quest, spoils));
        await Hook.AfterCardChangedPiles(run, spoils, PileType.None, null);
        Assert.Single(point.Quests, quest => ReferenceEquals(quest, spoils));
    }

    private static CardPlay Play(CardModel card, Player player, int spent, int value, bool auto) => new()
    {
        Card = card,
        Player = player,
        Target = null,
        ResultPile = PileType.Discard,
        Resources = new ResourceInfo(spent, value, 0, 0),
        IsAutoPlay = auto,
        PlayIndex = 0,
        PlayCount = 1,
    };

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed, int enemies)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() => Enumerable.Range(0, enemies)
            .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone())
            .ToArray());
        await room.Enter(run);
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToArray())
            {
                CardPileCmd.Remove(card);
            }
        }

        return (player, room);
    }
}
