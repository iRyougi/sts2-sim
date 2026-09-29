using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class IroncladRelicPotionBehaviorTests : IDisposable
{
    public IroncladRelicPotionBehaviorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("blood-potion")]
    [InlineData("soldiers-stew")]
    [InlineData("burning-blood")]
    [InlineData("charons-ashes")]
    [InlineData("brimstone")]
    [InlineData("red-skull")]
    [InlineData("ruined-helmet")]
    [InlineData("demon-tongue")]
    public async Task UseAndHooks_MatchSource(string behavior)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"ironclad-relic-potion-{behavior}");
        CombatState state = room.Engine.State;
        switch (behavior)
        {
            case "blood-potion":
                await CreatureCmd.Damage(state, [player.Creature], 40m,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                var blood = (BloodPotion)ModelDb.Potion<BloodPotion>().MutableClone();
                player.AddPotionInternal(blood);
                await PotionCmd.Use(blood, player, player.Creature);
                Assert.Equal(56, player.Creature.CurrentHp);
                Assert.DoesNotContain(blood, player.PotionSlots);
                break;
            case "soldiers-stew":
                StrikeIronclad strike = AddToHand<StrikeIronclad>(player);
                DefendIronclad defend = AddToHand<DefendIronclad>(player);
                var stew = (SoldiersStew)ModelDb.Potion<SoldiersStew>().MutableClone();
                player.AddPotionInternal(stew);
                await PotionCmd.Use(stew, player, player.Creature);
                Assert.Equal(1, strike.BaseReplayCount);
                Assert.Equal(0, defend.BaseReplayCount);
                break;
            case "burning-blood":
                await CreatureCmd.Damage(state, [player.Creature], 10m,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                await Hook.AfterCombatVictory(state);
                Assert.Equal(76, player.Creature.CurrentHp);
                break;
            case "charons-ashes":
                await RelicCmd.Obtain(ModelDb.Relic<CharonsAshes>(), player);
                var enemy = state.Enemies.Single();
                await CreatureCmd.SetCurrentHp(enemy, 3m);
                DefendIronclad untouched = AddToHand<DefendIronclad>(player);
                await CardPileCmd.Exhaust(state, AddToHand<DefendIronclad>(player));
                Assert.True(enemy.IsDead);
                await CardPileCmd.Exhaust(state, untouched);
                Assert.Contains(untouched, player.PlayerCombatState!.Hand.Cards);
                break;
            case "brimstone":
                await RelicCmd.Obtain(ModelDb.Relic<Brimstone>(), player);
                await player.Relics.OfType<Brimstone>().Single().AfterSideTurnStart(
                    CombatSide.Player, [player.Creature]);
                Assert.Equal(2m, player.Creature.GetPower<StrengthPower>()!.Amount);
                Assert.Equal(1m, state.Enemies.Single().GetPower<StrengthPower>()!.Amount);
                break;
            case "red-skull":
                await RelicCmd.Obtain(ModelDb.Relic<RedSkull>(), player);
                await CreatureCmd.Damage(state, [player.Creature], 40m,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                Assert.Equal(3m, player.Creature.GetPower<StrengthPower>()!.Amount);
                await RelicCmd.Obtain(ModelDb.Relic<DragonFruit>(), player);
                await PlayerCmd.GainGold(1m, player);
                Assert.Equal(81, player.Creature.MaxHp);
                Assert.Equal(41, player.Creature.CurrentHp);
                Assert.Equal(0m, player.Creature.GetPower<StrengthPower>()?.Amount ?? 0m);
                await CreatureCmd.Damage(state, [player.Creature], 1m,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                Assert.Equal(3m, player.Creature.GetPower<StrengthPower>()!.Amount);
                var juice = (FruitJuice)ModelDb.Potion<FruitJuice>().MutableClone();
                player.AddPotionInternal(juice);
                await PotionCmd.Use(juice, player, player.Creature);
                Assert.Equal(86, player.Creature.MaxHp);
                Assert.Equal(45, player.Creature.CurrentHp);
                Assert.Equal(0m, player.Creature.GetPower<StrengthPower>()?.Amount ?? 0m);
                break;
            case "ruined-helmet":
                await RelicCmd.Obtain(ModelDb.Relic<RuinedHelmet>(), player);
                await PowerCmd.Apply<StrengthPower>(state, player.Creature, 2m, player.Creature, null);
                await PowerCmd.Apply<StrengthPower>(state, player.Creature, 1m, player.Creature, null);
                Assert.Equal(5m, player.Creature.GetPower<StrengthPower>()!.Amount);
                break;
            case "demon-tongue":
                await RelicCmd.Obtain(ModelDb.Relic<DemonTongue>(), player);
                await CreatureCmd.Damage(state, [player.Creature], 3m,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                Assert.Equal(80, player.Creature.CurrentHp);
                await CreatureCmd.Damage(state, [player.Creature], 2m,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                Assert.Equal(78, player.Creature.CurrentHp);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(behavior));
        }
    }

    private static TCard AddToHand<TCard>(Player player) where TCard : CardModel
    {
        TCard card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player, CombatRoom)> CreateCombatAsync(string seed)
    {
        var run = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        foreach (var pile in player.PlayerCombatState!.AllPiles)
            foreach (CardModel card in pile.Cards.ToArray()) CardPileCmd.Remove(card);
        player.PlayerCombatState.Energy = 99;
        return (player, room);
    }
}
