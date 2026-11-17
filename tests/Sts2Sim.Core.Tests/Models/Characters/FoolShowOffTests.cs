using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Runs.Transplant;
using Sts2Sim.Core.Tests.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class FoolShowOffTests : IDisposable
{
    public FoolShowOffTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("growth_clone_rebind_and_downgrade")]
    [InlineData("minion_nonfatal_turnend_compare_projection_and_transplant")]
    public async Task ShowOff_DeckGrowthCloneCompareToDowngradeProjectionAndTransplant_MatchSource(string scenario)
    {
        var run = new RunState($"fool-g5-showoff-{scenario}",
            [new Overgrowth(), new Hive(), new Glory()], ascensionLevel: 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        var deckShowOff = (ShowOff)ModelDb.Card<ShowOff>().MutableClone();
        deckShowOff.AssignOwner(player);
        player.Deck.AddInternal(deckShowOff);
        if (scenario == "minion_nonfatal_turnend_compare_projection_and_transplant")
        {
            // Growth earned in an earlier combat is inherited by the combat copy of the next fight.
            deckShowOff.IncreaseAim(2);
        }

        switch (scenario)
        {
            case "growth_clone_rebind_and_downgrade":
                var room = new CombatRoom(() =>
                [
                    (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
                    (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
                ]);
                await room.Enter(run);
                CombatState state = room.Engine.State;
                PlayerCombatState pcs = player.PlayerCombatState!;
                pcs.Energy = 10;

                // Combat copy inherits DeckVersion reference set up during combat start from persistent Deck.
                ShowOff combatCopy = Assert.Single(pcs.AllPiles.SelectMany(p => p.Cards).OfType<ShowOff>());
                Assert.Same(deckShowOff, combatCopy.DeckVersion);

                combatCopy.Upgrade();
                await room.Engine.PlayCardAsync(player, AddToHand(player, combatCopy), null);
                Assert.Equal(5, player.Creature.GetPower<AimPower>()?.Amount ?? 0);
                ShowOffPower activePower = Assert.Single(player.Creature.Powers.OfType<ShowOffPower>());
                Assert.Equal(3, activePower.Amount);
                Assert.Same(combatCopy, activePower.ShowOffInstance);

                // Clone before enemy death; killing enemy[0] in clone must mutate clonedShowOff ONLY, leaving original unchanged!
                CombatState clone = state.Clone();
                Player clonePlayer = Assert.Single(clone.Players);
                ShowOff clonedCopy = Assert.Single(clonePlayer.PlayerCombatState!.AllPiles.SelectMany(p => p.Cards).OfType<ShowOff>());
                ShowOffPower clonedPower = Assert.Single(clonePlayer.Creature.Powers.OfType<ShowOffPower>());
                Assert.Same(clonedCopy, clonedPower.ShowOffInstance);
                Assert.NotSame(combatCopy, clonedCopy);

                clone.Enemies[0].SetCurrentHpInternal(1);
                await clone.Engine!.PlayCardAsync(clonePlayer, AddToPile<StrikeFool>(clonePlayer, PileType.Hand), clone.Enemies[0]);
                Assert.Equal(8, clonedCopy.CurrentAim);
                Assert.Equal(3, clonedCopy.IncreasedAim);
                Assert.Empty(clonePlayer.Creature.Powers.OfType<ShowOffPower>());
                // The cloned persistent deck card grows with the cloned combat copy; the originals stay untouched.
                ShowOff clonedDeckShowOff = Assert.Single(clonePlayer.Deck.Cards.OfType<ShowOff>());
                Assert.NotSame(deckShowOff, clonedDeckShowOff);
                Assert.Same(clonedDeckShowOff, clonedCopy.DeckVersion);
                Assert.Equal(8, clonedDeckShowOff.CurrentAim);
                Assert.Equal(3, clonedDeckShowOff.IncreasedAim);
                Assert.Equal(5, combatCopy.CurrentAim);
                Assert.Equal(5, deckShowOff.CurrentAim);

                // Downgrade preserves runtime Aim growth while reverting ShowOffPower visible stacks from 3 back to 2.
                clonedCopy.Downgrade();
                Assert.False(clonedCopy.IsUpgraded);
                Assert.Equal(8, clonedCopy.CurrentAim);
                VisibleContent downgradedVisible = VisibleContentProjection.ForCard(clonedCopy);
                Assert.Contains(VisibleValue.Int("AimPower", 8), downgradedVisible.Values);
                Assert.Contains(VisibleValue.Int("ShowOffPower", 2), downgradedVisible.Values);

                // Fatal death in original combat updates both combatCopy and persistent deckShowOff.
                state.Enemies[0].SetCurrentHpInternal(1);
                await room.Engine.PlayCardAsync(player, AddToPile<StrikeFool>(player, PileType.Hand), state.Enemies[0]);
                Assert.Equal(8, combatCopy.CurrentAim);
                Assert.Equal(3, combatCopy.IncreasedAim);
                Assert.Equal(8, deckShowOff.CurrentAim);
                Assert.Equal(3, deckShowOff.IncreasedAim);
                Assert.Empty(player.Creature.Powers.OfType<ShowOffPower>());
                break;

            case "minion_nonfatal_turnend_compare_projection_and_transplant":
                var room2 = new CombatRoom(() =>
                [
                    (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
                    (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
                ]);
                await room2.Enter(run);
                CombatState state2 = room2.Engine.State;
                PlayerCombatState pcs2 = player.PlayerCombatState!;
                pcs2.Energy = 10;

                // Non-fatal MinionPower suppresses ShowOffPower death growth; turn end removes ShowOffPower.
                ShowOff combat2 = Assert.Single(pcs2.AllPiles.SelectMany(p => p.Cards).OfType<ShowOff>());
                Assert.Same(deckShowOff, combat2.DeckVersion);
                Assert.Equal(7, combat2.CurrentAim);
                Assert.Equal(2, combat2.IncreasedAim);
                await room2.Engine.PlayCardAsync(player, AddToHand(player, combat2), null);
                await PowerCmd.Apply<MinionPower>(state2, state2.Enemies[0], 1m, null, null);
                state2.Enemies[0].SetCurrentHpInternal(1);
                await room2.Engine.PlayCardAsync(player, AddToPile<StrikeFool>(player, PileType.Hand), state2.Enemies[0]);
                Assert.Equal(7, combat2.CurrentAim);
                Assert.Equal(7, deckShowOff.CurrentAim);
                Assert.Single(player.Creature.Powers.OfType<ShowOffPower>());
                await room2.Engine.EndPlayerTurnAsync();
                Assert.Empty(player.Creature.Powers.OfType<ShowOffPower>());

                // CompareTo orders unscaled vs scaled ShowOff via ICardComparisonBaseValueSource ("AimPower").
                var lowShowOff = (ShowOff)ModelDb.Card<ShowOff>().MutableClone();
                var highShowOff = (ShowOff)ModelDb.Card<ShowOff>().MutableClone();
                highShowOff.IncreaseAim(2);
                Assert.True(lowShowOff.CompareTo(highShowOff) < 0);
                Assert.True(highShowOff.CompareTo(lowShowOff) > 0);
                Assert.Contains(VisibleValue.Int("AimPower", 7), VisibleContentProjection.ForCard(highShowOff).Values);

                // Act-entry transplant codec & RunState.CloneExact preserve deckShowOff.CurrentAim / IncreasedAim!
                var transplantRun = new RunState("fool-g5-transplant",
                    [new Overgrowth(), new Hive(), new Glory()], ascensionLevel: 10);
                Player tPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), transplantRun);
                transplantRun.AddPlayer(tPlayer);
                var savedCard = (ShowOff)ModelDb.Card<ShowOff>().MutableClone();
                savedCard.AssignOwner(tPlayer);
                savedCard.IncreaseAim(4);
                tPlayer.Deck.AddInternal(savedCard);
                transplantRun.AdvanceToNextAct();
                var boundary = await new RunDriver(transplantRun, new FirstChoiceDecisionSource())
                    .ContinueToActEntryAsync(1, 0);
                Assert.True(boundary.ReachedBoundary);
                string json = ActEntrySnapshotCodec.ExportActEntryJson(transplantRun);
                RunState imported = ActEntrySnapshotCodec.ImportActEntry(json);
                ShowOff importedShowOff = Assert.Single(imported.Players[0].Deck.Cards.OfType<ShowOff>());
                Assert.Equal(9, importedShowOff.CurrentAim);
                Assert.Equal(4, importedShowOff.IncreasedAim);
                ShowOff exactShowOff = Assert.Single(imported.CloneExact().Players[0].Deck.Cards.OfType<ShowOff>());
                Assert.Equal(9, exactShowOff.CurrentAim);
                Assert.Equal(4, exactShowOff.IncreasedAim);
                break;
        }
    }

    private static ShowOff AddToHand(Player player, ShowOff card)
    {
        if (card.Pile?.Type != PileType.Hand)
        {
            CardPileCmd.Add(card, PileType.Hand);
        }
        return card;
    }

    private static T AddToPile<T>(Player player, PileType pile) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }
}
