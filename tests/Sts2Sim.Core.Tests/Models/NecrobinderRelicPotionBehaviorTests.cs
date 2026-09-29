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
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class NecrobinderRelicPotionBehaviorTests : IDisposable
{
    public NecrobinderRelicPotionBehaviorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("bound-phylactery-bone-brew")]
    [InlineData("bookmark")]
    [InlineData("book-repair-knife-potion-of-doom")]
    [InlineData("bone-flute")]
    [InlineData("funerary-mask-pot-of-ghouls")]
    [InlineData("ivory-tile")]
    [InlineData("undying-sigil")]
    [InlineData("big-hat")]
    public async Task UseAndHooks_MatchSource(string behavior)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"necrobinder-relic-potion-{behavior}");
        CombatState state = room.Engine.State;
        Creature enemy = state.Enemies.Single();
        switch (behavior)
        {
            case "bound-phylactery-bone-brew":
                // Starting relic summoned 1 before combat; BoneBrew summons 15 onto the living Osty.
                Assert.Equal(1, player.Osty!.MaxHp);
                await UsePotion<BoneBrew>(player, player.Creature);
                Assert.Equal(16, player.Osty.MaxHp);
                Assert.Equal(16, player.Osty.CurrentHp);
                // From turn 2 on, AfterEnergyResetLate summons 1 more.
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(2, player.PlayerCombatState!.TurnNumber);
                Assert.Equal(17, player.Osty.MaxHp);
                break;
            case "bookmark":
            {
                await RelicCmd.Obtain(ModelDb.Relic<Bookmark>(), player);
                Reap reap = AddToHand<Reap>(player);
                AddToHand<StrikeNecrobinder>(player);
                int selections = state.RunState.Rng.CombatCardSelection.Counter;
                await room.Engine.EndPlayerTurnAsync();
                // Only the retained, non-X, positive-cost card is a candidate; the reduction lasts until played.
                Assert.Contains(reap, player.PlayerCombatState!.Hand.Cards);
                Assert.Equal(selections + 1, state.RunState.Rng.CombatCardSelection.Counter);
                Assert.Equal(2, reap.EnergyCost);
                await room.Engine.PlayCardAsync(player, reap, enemy);
                Assert.Equal(3, reap.EnergyCost);
                break;
            }
            case "book-repair-knife-potion-of-doom":
                await RelicCmd.Obtain(ModelDb.Relic<BookRepairKnife>(), player);
                await CreatureCmd.Damage(state, [player.Creature], 10m,
                    ValueProp.Unblockable | ValueProp.Unpowered, null, null, null);
                await CreatureCmd.SetCurrentHp(enemy, 30m);
                await UsePotion<PotionOfDoom>(player, enemy);
                Assert.Equal(33m, enemy.GetPower<DoomPower>()!.Amount);
                await Hook.BeforeSideTurnEnd(state, CombatSide.Enemy, state.Enemies.ToArray());
                Assert.True(enemy.IsDead);
                Assert.Equal(59, player.Creature.CurrentHp);
                break;
            case "bone-flute":
                await RelicCmd.Obtain(ModelDb.Relic<BoneFlute>(), player);
                await room.Engine.PlayCardAsync(player, AddToHand<StrikeNecrobinder>(player), enemy);
                Assert.Equal(0, player.Creature.Block);
                await room.Engine.PlayCardAsync(player, AddToHand<Unleash>(player), enemy);
                Assert.Equal(2, player.Creature.Block);
                break;
            case "funerary-mask-pot-of-ghouls":
            {
                await RelicCmd.Obtain(ModelDb.Relic<FuneraryMask>(), player);
                FuneraryMask mask = player.Relics.OfType<FuneraryMask>().Single();
                int shuffles = state.RunState.Rng.Shuffle.Counter;
                await mask.BeforeHandDraw(player);
                Assert.Equal(3, player.PlayerCombatState!.DrawPile.Cards.OfType<Soul>().Count());
                Assert.Equal(shuffles + 3, state.RunState.Rng.Shuffle.Counter);
                await UsePotion<PotOfGhouls>(player, player.Creature);
                Assert.Equal(2, player.PlayerCombatState.Hand.Cards.OfType<Soul>().Count());
                break;
            }
            case "ivory-tile":
                await RelicCmd.Obtain(ModelDb.Relic<IvoryTile>(), player);
                await room.Engine.PlayCardAsync(player, AddToHand<StrikeNecrobinder>(player), enemy);
                Assert.Equal(98, player.PlayerCombatState!.Energy);
                await room.Engine.PlayCardAsync(player, AddToHand<Reap>(player), enemy);
                Assert.Equal(96, player.PlayerCombatState.Energy);
                break;
            case "undying-sigil":
                await RelicCmd.Obtain(ModelDb.Relic<UndyingSigil>(), player);
                await CreatureCmd.Kill(player.Osty!);
                await CreatureCmd.SetCurrentHp(enemy, 5m);
                await PowerCmd.Apply<DoomPower>(state, enemy, 5m, player.Creature, null);
                await CreatureCmd.Damage(state, [player.Creature], 10m, ValueProp.Move, enemy, null, null);
                Assert.Equal(61, player.Creature.CurrentHp);
                await CreatureCmd.SetCurrentHp(enemy, 6m);
                await CreatureCmd.Damage(state, [player.Creature], 10m, ValueProp.Move, enemy, null, null);
                Assert.Equal(51, player.Creature.CurrentHp);
                break;
            case "big-hat":
            {
                await RelicCmd.Obtain(ModelDb.Relic<BigHat>(), player);
                BigHat hat = player.Relics.OfType<BigHat>().Single();
                int generations = state.RunState.Rng.CombatCardGeneration.Counter;
                await hat.AfterSideTurnStart(CombatSide.Player, [player.Creature]);
                CardModel[] hand = player.PlayerCombatState!.Hand.Cards.ToArray();
                Assert.Equal(2, hand.Length);
                Assert.Equal(2, hand.Select(card => card.GetType()).Distinct().Count());
                Assert.All(hand, card =>
                {
                    Assert.True(card.HasKeyword(CardKeyword.Ethereal));
                    Assert.Contains(card.GetType(),
                        ModelDb.Character<Necrobinder>().CardPool.AllCards.Select(pooled => pooled.GetType()));
                });
                Assert.True(state.RunState.Rng.CombatCardGeneration.Counter > generations);
                break;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(behavior));
        }
    }

    private static async Task UsePotion<TPotion>(Player player, Creature target) where TPotion : PotionModel
    {
        var potion = (TPotion)ModelDb.Potion<TPotion>().MutableClone();
        player.AddPotionInternal(potion);
        await PotionCmd.Use(potion, player, target);
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
        Player player = Player.CreateForNewRun(ModelDb.Character<Necrobinder>(), run);
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
