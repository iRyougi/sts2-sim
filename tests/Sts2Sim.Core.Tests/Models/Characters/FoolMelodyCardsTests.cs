using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class FoolMelodyCardsTests : IDisposable
{
    public FoolMelodyCardsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("lostlute_nested_first_play_and_tune_free_card")]
    [InlineData("cholimbs_coda_cost_reduction_and_kichant")]
    [InlineData("encore_limit_and_turn_end_removal")]
    public async Task MelodyCards_CostReductionsAndNestedReentry_MatchSource(string scenario)
    {
        var run = new RunState($"fool-g3-reentry-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        var room = new CombatRoom(() => (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone());
        await room.Enter(run);
        CombatState state = room.Engine.State;
        Creature enemy = Assert.Single(state.Enemies);
        enemy.SetMaxHpInternal(400);
        enemy.SetCurrentHpInternal(400);
        PlayerCombatState pcs = player.PlayerCombatState!;
        pcs.Energy = 20;

        switch (scenario)
        {
            case "lostlute_nested_first_play_and_tune_free_card":
                var probe = (FirstPlayCapturePower)new FirstPlayCapturePower().MutableClone();
                await PowerCmd.Apply(state, probe, player.Creature, 1m, player.Creature, null);
                await room.Engine.PlayCardAsync(player, AddToPile<LostLute>(player, PileType.Hand), null);

                // Put a Wah card on top of DrawPile so LostLute nested-autoplays it inside WahTouch's AfterCardPlayed.
                WahSweep nestedWah = AddToPile<WahSweep>(player, PileType.Draw, CardPilePosition.Top);
                WahTouch outerWah = AddToPile<WahTouch>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, outerWah, enemy);

                // Verify outer WahTouch got IsFirstPlayThisTurn == true while nested WahSweep got false!
                (CardModel Card, bool IsFirst) innerCapture = Assert.Single(probe.Captured, x => ReferenceEquals(x.Card, nestedWah));
                (CardModel Card, bool IsFirst) outerCapture = Assert.Single(probe.Captured, x => ReferenceEquals(x.Card, outerWah));
                Assert.False(innerCapture.IsFirst);
                Assert.True(outerCapture.IsFirst);

                // Play Tune and a Ki card, then end player turn -> TunePower.AfterSideTurnEnd grants FreeCardPower!
                await room.Engine.PlayCardAsync(player, AddToPile<Tune>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<KiWhip>(player, PileType.Hand), enemy);
                for (int i = 0; i < 6; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);

                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(1, player.Creature.GetPower<FreeCardPower>()?.Amount ?? 0);

                // FreeCardPower makes expensive card cost 0 on Turn 2 and decrements on play.
                WahBlast blast = AddToPile<WahBlast>(player, PileType.Hand);
                Assert.Equal(0, blast.EnergyCost);
                int energyBefore = pcs.Energy;
                await room.Engine.PlayCardAsync(player, blast, null);
                Assert.Equal(energyBefore, pcs.Energy);
                Assert.Empty(player.Creature.Powers.OfType<FreeCardPower>());
                break;

            case "cholimbs_coda_cost_reduction_and_kichant":
                ChoLimbs choLimbs = AddToPile<ChoLimbs>(player, PileType.Hand);
                Coda coda = AddToPile<Coda>(player, PileType.Hand);
                Assert.Equal(2, choLimbs.EnergyCost);
                Assert.Equal(6, coda.EnergyCost);

                await room.Engine.PlayCardAsync(player, AddToPile<WahTouch>(player, PileType.Hand), enemy);
                Assert.Equal(0, choLimbs.EnergyCost);
                Assert.Equal(4, coda.EnergyCost);

                await room.Engine.PlayCardAsync(player, choLimbs, null);
                Assert.Equal(10, player.Creature.Block);
                Assert.Equal(2, coda.EnergyCost);

                // KiChant inspects top 3 of draw, moves chosen to Hand and discards remaining (non-Hand Sly does not autoplay).
                StrikeFool d1 = AddToPile<StrikeFool>(player, PileType.Draw, CardPilePosition.Top);
                DefendFool d2 = AddToPile<DefendFool>(player, PileType.Draw, CardPilePosition.Top);
                DefendFool d3 = AddToPile<DefendFool>(player, PileType.Draw, CardPilePosition.Top);
                state.CardSelectionSource = new SpecificCardSelector(d2);
                await room.Engine.PlayCardAsync(player, AddToPile<KiChant>(player, PileType.Hand), null);
                Assert.Contains(d2, pcs.Hand.Cards);
                Assert.Contains(d1, pcs.DiscardPile.Cards);
                Assert.Contains(d3, pcs.DiscardPile.Cards);
                Assert.Equal(0, coda.EnergyCost);

                int hpBeforeCoda = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, coda, enemy);
                // WahTouch applied VulnerablePower on enemy, so Coda's 40 damage deals 60.
                Assert.Equal(60, hpBeforeCoda - enemy.CurrentHp);
                break;

            case "encore_limit_and_turn_end_removal":
                AddToPile<DefendFool>(player, PileType.Draw);
                AddToPile<DefendFool>(player, PileType.Draw);
                for (int i = 0; i < 6; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);

                await room.Engine.PlayCardAsync(player, AddToPile<Encore>(player, PileType.Hand), null);
                Assert.Equal(1, player.Creature.GetPower<EncorePower>()?.DisplayAmount);

                // Can play 1 more card, then EncorePower blocks a 2nd card!
                StrikeFool allowed = AddToPile<StrikeFool>(player, PileType.Hand);
                StrikeFool blocked = AddToPile<StrikeFool>(player, PileType.Hand);
                Assert.NotNull(await room.Engine.PlayCardWithResultAsync(player, allowed, enemy));
                Assert.Equal(0, player.Creature.GetPower<EncorePower>()?.DisplayAmount);
                Assert.False(blocked.CanPlay(out _));

                CombatState cloneWithEncore = state.Clone();
                Assert.Equal(0, Assert.Single(cloneWithEncore.Players).Creature.GetPower<EncorePower>()?.DisplayAmount);

                await room.Engine.EndPlayerTurnAsync();
                Assert.Empty(player.Creature.Powers.OfType<EncorePower>());
                break;
        }
    }

    [Theory]
    [InlineData("lullaby_kiverse_songpath_violentchord_motif")]
    [InlineData("chobarbs_chokichorus_chofirm_scaling_and_ensemble")]
    public async Task MelodyPowersAndScalingCards_MatchSource(string scenario)
    {
        var run = new RunState($"fool-g3-powers-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        int dummyCount = scenario == "lullaby_kiverse_songpath_violentchord_motif" ? 2 : 1;
        var room = new CombatRoom(() => Enumerable.Range(0, dummyCount)
            .Select(_ => (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone())
            .ToArray());
        await room.Enter(run);
        CombatState state = room.Engine.State;
        foreach (Creature e in state.Enemies)
        {
            e.SetMaxHpInternal(400);
            e.SetCurrentHpInternal(400);
        }
        Creature enemy = state.Enemies[0];
        PlayerCombatState pcs = player.PlayerCombatState!;
        pcs.Energy = 30;

        switch (scenario)
        {
            case "lullaby_kiverse_songpath_violentchord_motif":
                AddToPile<DefendFool>(player, PileType.Draw);
                AddToPile<DefendFool>(player, PileType.Draw);
                AddToPile<DefendFool>(player, PileType.Draw);
                await room.Engine.PlayCardAsync(player, AddToPile<Lullaby>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<SongPath>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<ViolentChord>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<Motif>(player, PileType.Hand), null);

                // First Wah triggers ViolentChord (9 AoE) + Motif draw 1.
                int enemy2Before = state.Enemies[1].CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<WahTouch>(player, PileType.Hand), enemy);
                Assert.Equal(9, enemy2Before - state.Enemies[1].CurrentHp);

                // KiVerse doubles next melody card play count (ChoKi gives 8 Block twice + triggers SongPath 5 + Lullaby 20!).
                await room.Engine.PlayCardAsync(player, AddToPile<KiVerse>(player, PileType.Hand), null);
                int blockBefore = player.Creature.Block;
                await room.Engine.PlayCardAsync(player, AddToPile<ChoKi>(player, PileType.Hand), null);
                // ChoKi replays twice (8+8=16 Block) + SongPath (5 Block, first Cho) + Lullaby (20 Block, all 3 melodies!).
                Assert.Equal(16 + 5 + 20, player.Creature.Block - blockBefore);
                Assert.Empty(player.Creature.Powers.OfType<KiVersePower>());
                break;

            case "chobarbs_chokichorus_chofirm_scaling_and_ensemble":
                // ChoBarbs grants Block + TemporaryThornsPower -> ThornsPower until enemy turn end.
                await room.Engine.PlayCardAsync(player, AddToPile<ChoBarbs>(player, PileType.Hand), null);
                Assert.Equal(7, player.Creature.Block);
                Assert.Equal(3, player.Creature.GetPower<ThornsPower>()?.Amount ?? 0);

                // ChoFirm doubles block when player has an arrow.
                pcs.GainArrow();
                int blockBeforeFirm = player.Creature.Block;
                await room.Engine.PlayCardAsync(player, AddToPile<ChoFirm>(player, PileType.Hand), null);
                Assert.Equal(12, player.Creature.Block - blockBeforeFirm);

                // ChoChorus auto-plays Wah/Ki cards in Hand so all three melodies (Wah, Cho, Ki) are active.
                WahCho handWahCho = AddToPile<WahCho>(player, PileType.Hand);
                AddToPile<KiWhip>(player, PileType.Hand);
                int enemyBeforeChorus = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<ChoChorus>(player, PileType.Hand), null);
                Assert.True(enemyBeforeChorus - enemy.CurrentHp >= 3);

                // WahSymphony scales with melody cards across combat piles; RhythmStrike hits 1 + MelodiesThisTurn; HarmonicShield scales block.
                int hpBeforeSym = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<WahSymphony>(player, PileType.Hand), null);
                Assert.True(hpBeforeSym - enemy.CurrentHp > 6);

                int hpBeforeRhythm = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<RhythmStrike>(player, PileType.Hand), enemy);
                // 3 melodies played (Wah, Cho, Ki) -> 1 + 3 = 4 hits * 4 = 16 damage.
                Assert.Equal(16, hpBeforeRhythm - enemy.CurrentHp);

                int blockBeforeHarm = player.Creature.Block;
                await room.Engine.PlayCardAsync(player, AddToPile<HarmonicShield>(player, PileType.Hand), null);
                Assert.Equal(15, player.Creature.Block - blockBeforeHarm);

                // WahHammer doubles damage against impaled enemy.
                pcs.GainArrow();
                await room.Engine.PlayCardAsync(player, AddToPile<FoolishShot>(player, PileType.Hand), enemy);
                int hpBeforeHammer = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<WahHammer>(player, PileType.Hand), enemy);
                Assert.Equal(18, hpBeforeHammer - enemy.CurrentHp);

                Assert.True(ModelDb.Card<Ensemble>().IsMultiplayerOnly);

                for (int i = 0; i < 6; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(0, player.Creature.GetPower<ThornsPower>()?.Amount ?? 0);
                break;
        }
    }

    private static T AddToPile<T>(Player player, PileType pile, CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile, position);
        return card;
    }

    private sealed class SpecificCardSelector(CardModel chosen) : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>(
                request.Candidates.Contains(chosen) ? [chosen] : request.Candidates.Take(request.MinCount).ToArray());
    }

    private sealed class FirstPlayCapturePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public List<(CardModel Card, bool IsFirst)> Captured { get; } = [];

        public override Task AfterCardPlayed(CardPlay cardPlay)
        {
            Captured.Add((cardPlay.Card, MelodyCmd.IsFirstPlayThisTurn(cardPlay, CardTag.Wah)));
            return Task.CompletedTask;
        }
    }
}
