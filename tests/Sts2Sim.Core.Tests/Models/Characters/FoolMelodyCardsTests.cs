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
    [InlineData("lostlute_nested_first_play_and_tune_upgrade")]
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
            case "lostlute_nested_first_play_and_tune_upgrade":
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

                // Remove LostLute so later Ki/Wah plays do not autoplay from the draw pile.
                await PowerCmd.Remove(player.Creature.GetPower<LostLutePower>()!);
                foreach (CardModel c in pcs.Hand.Cards.ToList())
                    CardPileCmd.Remove(c);

                // v0.114.0 TunePower: the first Ki play this turn upgrades Amount random upgradable hand cards (CombatCardSelection).
                Tune tune = AddToPile<Tune>(player, PileType.Hand);
                int energyBeforeTune = pcs.Energy;
                await room.Engine.PlayCardAsync(player, tune, null);
                Assert.Equal(energyBeforeTune - 1, pcs.Energy);
                Assert.Equal(1, player.Creature.GetPower<TunePower>()?.Amount ?? 0);
                StrikeFool tuned = AddToPile<StrikeFool>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, AddToPile<KiWhip>(player, PileType.Hand), enemy);
                Assert.True(tuned.IsUpgraded);
                StrikeFool notTuned = AddToPile<StrikeFool>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, AddToPile<KiWhip>(player, PileType.Hand), enemy);
                Assert.False(notTuned.IsUpgraded);
                for (int i = 0; i < 6; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);

                await room.Engine.EndPlayerTurnAsync();

                WahBlast blastSingleHit = AddToPile<WahBlast>(player, PileType.Hand);
                int hpBeforeBlastSingle = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, blastSingleHit, null);
                Assert.Equal(14, hpBeforeBlastSingle - enemy.CurrentHp);

                // v0.114.0 WahKi: 3x2 damage, then put 1 chosen discard card on top of the draw pile; subsequent WahBlast hits twice for 28!
                Assert.Contains(notTuned, pcs.DiscardPile.Cards);
                state.CardSelectionSource = new SpecificCardSelector(notTuned);
                int hpBeforeWahKi = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<WahKi>(player, PileType.Hand), enemy);
                Assert.Equal(6, hpBeforeWahKi - enemy.CurrentHp);
                Assert.Same(notTuned, pcs.DrawPile.Cards[0]);
                Assert.Empty(player.Creature.Powers.OfType<EnergyNextTurnPower>());

                int hpBeforeBlastDouble = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<WahBlast>(player, PileType.Hand), null);
                Assert.Equal(28, hpBeforeBlastDouble - enemy.CurrentHp);
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
                foreach (CardModel c in pcs.Hand.Cards.ToList())
                    CardPileCmd.Remove(c);
                for (int i = 0; i < 4; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);

                // v0.114.0 WahChoKi (2 cost): 8 Damage then 8 Block, draws 1, and completes all 3 melodies for SteadyCadence (10 Damage + 3 Draw).
                int hpBeforeWahChoKi = enemy.CurrentHp;
                int energyBeforeWahChoKi = pcs.Energy;
                await room.Engine.PlayCardAsync(player, AddToPile<WahChoKi>(player, PileType.Hand), enemy);
                Assert.Equal(energyBeforeWahChoKi - 2, pcs.Energy);
                Assert.Equal(8, player.Creature.Block);
                Assert.Equal(8, hpBeforeWahChoKi - enemy.CurrentHp);
                Assert.Single(pcs.Hand.Cards);
                Assert.Equal(3, MelodyCmd.MelodiesThisTurn(player));

                int hpBeforeCadence = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<SteadyCadence>(player, PileType.Hand), enemy);
                Assert.Equal(10, hpBeforeCadence - enemy.CurrentHp);
                Assert.Equal(4, pcs.Hand.Cards.Count);

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

                // First Wah triggers ViolentChord (v0.114.0: 7 AoE) + Motif draw 1.
                int enemy2Before = state.Enemies[1].CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<WahTouch>(player, PileType.Hand), enemy);
                Assert.Equal(7, enemy2Before - state.Enemies[1].CurrentHp);

                // KiVerse doubles next melody card play count (ChoKi gives 8 Block twice + triggers SongPath 5 + Lullaby 20!).
                await room.Engine.PlayCardAsync(player, AddToPile<KiVerse>(player, PileType.Hand), null);
                int blockBefore = player.Creature.Block;
                await room.Engine.PlayCardAsync(player, AddToPile<ChoKi>(player, PileType.Hand), null);
                // ChoKi (Cho counts for KiVerse) replays twice (8+8=16 Block) + SongPath (v0.114.0: 4) + Lullaby (v0.114.0: 12).
                Assert.Equal(16 + 4 + 12, player.Creature.Block - blockBefore);
                Assert.Empty(player.Creature.Powers.OfType<KiVersePower>());

                // v0.114.0 KiVerse no longer replays a Ki-only card.
                await room.Engine.PlayCardAsync(player, AddToPile<KiVerse>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<KiWhip>(player, PileType.Hand), enemy);
                Assert.Equal(1, player.Creature.GetPower<KiVersePower>()?.Amount ?? 0);
                break;

            case "chobarbs_chokichorus_chofirm_scaling_and_ensemble":
                // ChoBarbs grants Block + TemporaryThornsPower -> ThornsPower until enemy turn end.
                await room.Engine.PlayCardAsync(player, AddToPile<ChoBarbs>(player, PileType.Hand), null);
                Assert.Equal(8, player.Creature.Block);
                Assert.Equal(2, player.Creature.GetPower<ThornsPower>()?.Amount ?? 0);

                // ChoFirm doubles block when player has an arrow.
                pcs.GainArrow();
                int blockBeforeFirm = player.Creature.Block;
                await room.Engine.PlayCardAsync(player, AddToPile<ChoFirm>(player, PileType.Hand), null);
                Assert.Equal(10, player.Creature.Block - blockBeforeFirm);

                // ChoChorus auto-plays WahCho (3 Block + 3 Damage) + KiWhip (5 Damage) -> exact 8 Damage across all 3 melodies.
                WahCho handWahCho = AddToPile<WahCho>(player, PileType.Hand);
                AddToPile<KiWhip>(player, PileType.Hand);
                int enemyBeforeChorus = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<ChoChorus>(player, PileType.Hand), null);
                Assert.Equal(8, enemyBeforeChorus - enemy.CurrentHp);

                // v0.114.0 WahSymphony scales with Cho/Ki cards only (ChoBarbs, ChoFirm, WahCho, KiWhip, ChoChorus = 5 -> 6 + 4*5 = 26).
                WahSymphony symphony = AddToPile<WahSymphony>(player, PileType.Hand);
                int choKiCardCount = pcs.AllPiles
                    .SelectMany(p => p.Cards)
                    .Count(c => c.Tags.Contains(CardTag.Cho) || c.Tags.Contains(CardTag.Ki));
                Assert.Equal(5, choKiCardCount);
                int hpBeforeSym = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, symphony, null);
                Assert.Equal(26, hpBeforeSym - enemy.CurrentHp);

                int hpBeforeRhythm = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<RhythmStrike>(player, PileType.Hand), enemy);
                // 3 melodies played (Wah, Cho, Ki) -> 1 + 3 = 4 hits * 4 = 16 damage.
                Assert.Equal(16, hpBeforeRhythm - enemy.CurrentHp);

                int blockBeforeHarm = player.Creature.Block;
                await room.Engine.PlayCardAsync(player, AddToPile<HarmonicShield>(player, PileType.Hand), null);
                // v0.114.0 HarmonicShield: 4 + 4 per melody (3) = 16.
                Assert.Equal(16, player.Creature.Block - blockBeforeHarm);

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
