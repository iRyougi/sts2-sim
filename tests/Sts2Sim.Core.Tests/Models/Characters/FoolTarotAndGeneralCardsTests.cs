using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class FoolTarotAndGeneralCardsTests : IDisposable
{
    public FoolTarotAndGeneralCardsTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("tarot_list_hanged_man_hierophant_fool_combat")]
    [InlineData("tarot_generation_transform_and_utility")]
    public async Task TarotCards_AllAndEffects_MatchSource(string scenario)
    {
        var run = new RunState($"fool-g4-tarot-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        if (scenario == "tarot_list_hanged_man_hierophant_fool_combat")
        {
            for (int row = 0; row < 5; row++)
                run.AddVisitedMapCoord(new MapCoord(0, row));
            Assert.Equal(5, run.TotalFloor);
        }

        var room = new CombatRoom(() =>
        [
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
        ]);
        await room.Enter(run);
        CombatState state = room.Engine.State;
        Creature enemy = state.Enemies[0];
        Creature enemy2 = state.Enemies[1];
        enemy.SetMaxHpInternal(500);
        enemy.SetCurrentHpInternal(500);
        enemy2.SetMaxHpInternal(500);
        enemy2.SetCurrentHpInternal(500);
        PlayerCombatState pcs = player.PlayerCombatState!;
        pcs.Energy = 20;
        foreach (CardModel c in pcs.Hand.Cards.ToList())
            CardPileCmd.Remove(c);

        switch (scenario)
        {
            case "tarot_list_hanged_man_hierophant_fool_combat":
                Type[] expectedTarotOrder =
                [
                    typeof(Judgement), typeof(TheFool), typeof(TheHighPriestess), typeof(TheEmperor),
                    typeof(TheEmpress), typeof(TheHangedMan), typeof(TheHierophant), typeof(TheChariot),
                    typeof(Justice), typeof(TheMagician), typeof(TheMoon), typeof(TheTower),
                    typeof(Strength), typeof(Death), typeof(Temperance), typeof(TheDevil),
                    typeof(TheHermit), typeof(TheLovers), typeof(TheStar), typeof(TheSun),
                    typeof(TheWorld), typeof(WheelOfFortune),
                ];
                Assert.Equal(expectedTarotOrder, TarotCards.All.Select(c => c.GetType()));

                // Test AddEnergyCostThisTurnOrUntilPlayed + TheHierophant.
                StrikeFool testAddCost = AddToPile<StrikeFool>(player, PileType.Hand);
                StrikeFool testPlayClear = AddToPile<StrikeFool>(player, PileType.Hand);
                testAddCost.AddEnergyCostThisTurnOrUntilPlayed(1);
                testPlayClear.AddEnergyCostThisTurnOrUntilPlayed(-1);
                Assert.Equal(2, testAddCost.EnergyCost);
                Assert.Equal(0, testPlayClear.EnergyCost);
                await room.Engine.PlayCardAsync(player, testPlayClear, enemy);
                Assert.Equal(1, testPlayClear.EnergyCost);

                int handBeforeHierophant = pcs.Hand.Cards.Count;
                await room.Engine.PlayCardAsync(player, AddToPile<TheHierophant>(player, PileType.Hand), null);
                Assert.Equal(handBeforeHierophant + 3, pcs.Hand.Cards.Count);
                CardModel[] hierophantCards = pcs.Hand.Cards.Skip(handBeforeHierophant).ToArray();
                Assert.Equal(3, hierophantCards.Length);
                // v0.114.0 TheHierophant no longer reduces the generated cards' cost.
                Assert.All(hierophantCards, c =>
                {
                    Assert.True(c.IsColorless);
                    Assert.Equal(c.CanonicalEnergyCostValue, c.EnergyCost);
                });

                // TheEmpress, Strength, Temperance, Justice, TheChariot, TheTower, TheEmperor, TheHighPriestess.
                await room.Engine.PlayCardAsync(player, AddToPile<Strength>(player, PileType.Hand), null);
                Assert.Equal(4, player.Creature.GetPower<StrengthPower>()?.Amount ?? 0);
                await room.Engine.PlayCardAsync(player, AddToPile<Temperance>(player, PileType.Hand), null);
                Assert.Equal(20, player.Creature.Block);
                await room.Engine.PlayCardAsync(player, AddToPile<TheEmpress>(player, PileType.Hand), enemy);
                Assert.Equal(3, enemy.GetPower<WeakPower>()?.Amount ?? 0);
                Assert.Equal(3, enemy.GetPower<VulnerablePower>()?.Amount ?? 0);
                int e2BeforeChariot = enemy2.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<TheChariot>(player, PileType.Hand), null);
                Assert.Equal(29, e2BeforeChariot - enemy2.CurrentHp);

                int energyBeforeEmperor = pcs.Energy;
                await room.Engine.PlayCardAsync(player, AddToPile<TheEmperor>(player, PileType.Hand), null);
                Assert.Equal(energyBeforeEmperor + 3, pcs.Energy);

                for (int i = 0; i < 4; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);
                int handBeforePriestess = pcs.Hand.Cards.Count;
                await room.Engine.PlayCardAsync(player, AddToPile<TheHighPriestess>(player, PileType.Hand), null);
                Assert.Equal(handBeforePriestess + 4, pcs.Hand.Cards.Count);

                await room.Engine.PlayCardAsync(player, AddToPile<Death>(player, PileType.Hand), enemy2);
                Assert.Equal(66, enemy2.GetPower<DoomPower>()?.Amount ?? 0);

                int e2BeforeJustice = enemy2.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<Justice>(player, PileType.Hand), enemy2);
                Assert.Equal(34, e2BeforeJustice - enemy2.CurrentHp);

                int e2BeforeTower = enemy2.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<TheTower>(player, PileType.Hand), enemy2);
                // v0.114.0 TheTower: floor 5 x 1 + 4 Strength = 9.
                Assert.Equal(9, e2BeforeTower - enemy2.CurrentHp);

                // Judgement only executes <= Threshold HP (30 unupgraded) and runs full death resolution via SetCurrentHp.
                CompostPower compost = (await PowerCmd.Apply<CompostPower>(
                    state, player.Creature, 1m, player.Creature, null))!;
                await PowerCmd.Apply<WeakPower>(state, enemy2, 2m, player.Creature, null);
                int energyBeforeJudgement = pcs.Energy;
                enemy2.SetCurrentHpInternal(31);
                await room.Engine.PlayCardAsync(player, AddToPile<Judgement>(player, PileType.Hand), enemy2);
                Assert.True(enemy2.IsAlive);
                Assert.Contains(enemy2, state.Enemies);
                Assert.NotEmpty(enemy2.Powers);
                Assert.Equal(energyBeforeJudgement, pcs.Energy);
                enemy2.SetCurrentHpInternal(30);
                await room.Engine.PlayCardAsync(player, AddToPile<Judgement>(player, PileType.Hand), enemy2);
                Assert.True(enemy2.IsDead);
                Assert.DoesNotContain(enemy2, state.Enemies);
                Assert.Empty(enemy2.Powers);
                Assert.Equal(energyBeforeJudgement + 1, pcs.Energy);
                await PowerCmd.Remove(compost);

                // TheHangedMan deals 6 unblockable HP loss and grants ExtraTurnPower.
                int hpBeforeHanged = player.Creature.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<TheHangedMan>(player, PileType.Hand), null);
                Assert.Equal(6, hpBeforeHanged - player.Creature.CurrentHp);
                Assert.Equal(1, player.Creature.GetPower<ExtraTurnPower>()?.Amount ?? 0);

                for (int i = 0; i < 6; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);
                await room.Engine.EndPlayerTurnAsync();
                Assert.True(state.IsPlayerExtraTurn);
                Assert.Equal(CombatSide.Player, state.CurrentSide);
                Assert.Equal(2, pcs.TurnNumber);
                Assert.Equal(player.MaxEnergy, pcs.Energy);
                // AddThisTurnOrUntilPlayed cleared on turn end too.
                Assert.Equal(1, testAddCost.EnergyCost);
                Assert.All(hierophantCards, c => Assert.Equal(c.CanonicalEnergyCostValue, c.EnergyCost));
                break;

            case "tarot_generation_transform_and_utility":
                // TheMagician adds 3 orb slots and channels 3 orbs.
                await room.Engine.PlayCardAsync(player, AddToPile<TheMagician>(player, PileType.Hand), null);
                Assert.Equal(3, pcs.OrbQueue.Capacity);
                Assert.Equal(3, pcs.OrbQueue.Orbs.Count);

                // TheStar grants EnergyNextTurnPower & DrawCardsNextTurnPower.
                await room.Engine.PlayCardAsync(player, AddToPile<TheStar>(player, PileType.Hand), null);
                Assert.Equal(3, player.Creature.GetPower<EnergyNextTurnPower>()?.Amount ?? 0);
                Assert.Equal(3, player.Creature.GetPower<DrawCardsNextTurnPower>()?.Amount ?? 0);

                // v0.114.0 TheSun draws 2, gains 2 energy, shuffles 2 Soot into random draw-pile positions.
                AddToPile<DefendFool>(player, PileType.Draw);
                AddToPile<DefendFool>(player, PileType.Draw);
                int eBeforeSun = pcs.Energy;
                await room.Engine.PlayCardAsync(player, AddToPile<TheSun>(player, PileType.Hand), null);
                Assert.Equal(eBeforeSun + 2, pcs.Energy);
                Assert.Equal(2, pcs.DrawPile.Cards.OfType<Soot>().Count());
                Assert.Empty(pcs.DiscardPile.Cards.OfType<Soot>());

                // TheHermit discards non-Curse, exhausts Curse; TheWorld pulls from Discard.
                foreach (CardModel c in pcs.Hand.Cards.ToList())
                    CardPileCmd.Remove(c);
                AddToPile<DefendFool>(player, PileType.Draw);
                AddToPile<DefendFool>(player, PileType.Draw);
                Injury curseInHand = AddToPile<Injury>(player, PileType.Hand);
                state.CardSelectionSource = new SpecificCardSelector(curseInHand);
                await room.Engine.PlayCardAsync(player, AddToPile<TheHermit>(player, PileType.Hand), null);
                Assert.Contains(curseInHand, pcs.ExhaustPile.Cards);

                // WheelOfFortune discards hand and draws 7 (or draws when empty).
                for (int i = 0; i < 10; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);
                await room.Engine.PlayCardAsync(player, AddToPile<WheelOfFortune>(player, PileType.Hand), null);
                Assert.Equal(7, pcs.Hand.Cards.Count);

                // TheWorld brings back exact 3 cards from Discard up to hand limit (7 -> 10).
                int handBeforeWorld = pcs.Hand.Cards.Count;
                await room.Engine.PlayCardAsync(player, AddToPile<TheWorld>(player, PileType.Hand), null);
                Assert.Equal(handBeforeWorld + 3, pcs.Hand.Cards.Count);

                // TheLovers fills Hand (6 -> 10) from other character card pools.
                for (int i = 0; i < 4; i++)
                    CardPileCmd.Remove(pcs.Hand.Cards[^1]);
                await room.Engine.PlayCardAsync(player, AddToPile<TheLovers>(player, PileType.Hand), null);
                Assert.Equal(CardPile.MaxCardsInHand, pcs.Hand.Cards.Count);
                HashSet<ModelId> silentPoolIds = ModelDb.Character<Silent>().CardPool.AllCards.Select(c => c.Id).ToHashSet();
                Assert.All(pcs.Hand.Cards.Skip(6), c => Assert.DoesNotContain(c.Id, silentPoolIds));

                // TheFool autoplays playable cards from Hand rather than Unplayable Sadness.
                foreach (CardModel c in pcs.Hand.Cards.ToList())
                    CardPileCmd.Remove(c);
                AddToPile<Sadness>(player, PileType.Hand);
                DefendFool playable1 = AddToPile<DefendFool>(player, PileType.Hand);
                DefendFool playable2 = AddToPile<DefendFool>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, AddToPile<TheFool>(player, PileType.Hand), null);
                Assert.Contains(playable1, pcs.DiscardPile.Cards);
                Assert.Contains(playable2, pcs.DiscardPile.Cards);
                Assert.Single(pcs.Hand.Cards.OfType<Sadness>());
                // v0.114.0 removed the all-hand fallback: with only Unplayable cards TheFool autoplays nothing.
                int discardBeforeLoneFool = pcs.DiscardPile.Cards.Count;
                TheFool loneFool = AddToPile<TheFool>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, loneFool, null);
                Assert.Single(pcs.Hand.Cards.OfType<Sadness>());
                Assert.Equal(discardBeforeLoneFool, pcs.DiscardPile.Cards.Count);

                // TheMoon+ fills Hand with 10 upgraded Shivs; TheDevil+ exhausts 2 cards & autoplays/upgrades exact 2 from top of Draw.
                foreach (CardModel c in pcs.Hand.Cards.ToList())
                    CardPileCmd.Remove(c);
                TheMoon moon = AddToPile<TheMoon>(player, PileType.Hand);
                moon.Upgrade();
                await room.Engine.PlayCardAsync(player, moon, null);
                Assert.Equal(CardPile.MaxCardsInHand, pcs.Hand.Cards.OfType<Shiv>().Count());
                Assert.All(pcs.Hand.Cards.OfType<Shiv>(), s => Assert.True(s.IsUpgraded));

                foreach (CardModel c in pcs.Hand.Cards.ToList())
                    CardPileCmd.Remove(c);
                foreach (CardModel c in pcs.DrawPile.Cards.ToList())
                    CardPileCmd.Remove(c);
                StrikeFool devilHand1 = AddToPile<StrikeFool>(player, PileType.Hand);
                StrikeFool devilHand2 = AddToPile<StrikeFool>(player, PileType.Hand);
                DefendFool devilDraw1 = AddToPile<DefendFool>(player, PileType.Draw);
                DefendFool devilDraw2 = AddToPile<DefendFool>(player, PileType.Draw);
                DefendFool devilDraw3 = AddToPile<DefendFool>(player, PileType.Draw);
                TheDevil devil = AddToPile<TheDevil>(player, PileType.Hand);
                devil.Upgrade();
                int blockBeforeDevil = player.Creature.Block;
                int exhaustBeforeDevil = pcs.ExhaustPile.Cards.Count;
                await room.Engine.PlayCardAsync(player, devil, null);
                Assert.Empty(pcs.Hand.Cards);
                Assert.Equal(exhaustBeforeDevil + 3, pcs.ExhaustPile.Cards.Count);
                Assert.Contains(devilHand1, pcs.ExhaustPile.Cards);
                Assert.Contains(devilHand2, pcs.ExhaustPile.Cards);
                Assert.Equal(16, player.Creature.Block - blockBeforeDevil);
                Assert.True(devilDraw1.IsUpgraded);
                Assert.True(devilDraw2.IsUpgraded);
                Assert.False(devilDraw3.IsUpgraded);
                Assert.Contains(devilDraw1, pcs.DiscardPile.Cards);
                Assert.Contains(devilDraw2, pcs.DiscardPile.Cards);
                Assert.Same(devilDraw3, Assert.Single(pcs.DrawPile.Cards));
                break;
        }
    }

    [Theory]
    [InlineData("sadness_generation_anguish_ecstasy_ennui_all_is_well")]
    [InlineData("mythicform_simpleton_timber_lockedin_dislikeyou_multiplayer")]
    public async Task PoolCardsAndPowers_SadnessEngine_MatchSource(string scenario)
    {
        var run = new RunState($"fool-g4-pool-{scenario}", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);

        int enemyCount = scenario == "mythicform_simpleton_timber_lockedin_dislikeyou_multiplayer" ? 2 : 1;
        var room = new CombatRoom(() => Enumerable.Range(0, enemyCount)
            .Select(_ => (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone())
            .ToArray());
        await room.Enter(run);
        CombatState state = room.Engine.State;
        foreach (Creature e in state.Enemies)
        {
            e.SetMaxHpInternal(500);
            e.SetCurrentHpInternal(500);
        }
        Creature enemy = state.Enemies[0];
        PlayerCombatState pcs = player.PlayerCombatState!;
        pcs.Energy = 30;
        foreach (CardModel c in pcs.Hand.Cards.ToList())
            CardPileCmd.Remove(c);

        switch (scenario)
        {
            case "sadness_generation_anguish_ecstasy_ennui_all_is_well":
                for (int i = 0; i < 12; i++)
                    AddToPile<DefendFool>(player, PileType.Draw);

                await room.Engine.PlayCardAsync(player, AddToPile<AllIsWell>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<Jest>(player, PileType.Hand), null);
                // v0.114.0: 15 Block (Jest) + 4 Block (AllIsWell triggered by 1 Sadness).
                Assert.Equal(19, player.Creature.Block);
                Assert.Equal(1, pcs.SadnessGeneratedThisCombat);

                await room.Engine.PlayCardAsync(player, AddToPile<Rummage>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<Vitriol>(player, PileType.Hand), enemy);
                await room.Engine.PlayCardAsync(player, AddToPile<SorrowfulStrike>(player, PileType.Hand), enemy);
                await room.Engine.PlayCardAsync(player, AddToPile<BrightenUp>(player, PileType.Hand), null);
                Assert.Equal(7, pcs.SadnessGeneratedThisCombat);

                // Keep room in Hand so transformed Sadness (MourningBlow) & Turn 2 Ennui/Ecstasy stay in Hand cleanly.
                foreach (CardModel c in pcs.Hand.Cards.OfType<DefendFool>().ToList())
                    CardPileCmd.Remove(c);

                // PunchOfJoy transforms 1 Hand Sadness -> Joy.
                int joyBeforePunch = pcs.Hand.Cards.OfType<Joy>().Count();
                await room.Engine.PlayCardAsync(player, AddToPile<PunchOfJoy>(player, PileType.Hand), enemy);
                Assert.Equal(joyBeforePunch + 1, pcs.Hand.Cards.OfType<Joy>().Count());

                // MourningBlow transforms after play into Sadness in Hand and increments SadnessGeneratedThisCombat!
                MourningBlow mourning = AddToPile<MourningBlow>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, mourning, enemy);
                Assert.DoesNotContain(mourning, pcs.AllPiles.SelectMany(p => p.Cards));
                Assert.Equal(8, pcs.SadnessGeneratedThisCombat);
                Assert.Contains(pcs.Hand.Cards, c => c is Sadness && ReferenceEquals(c, pcs.Hand.Cards[^1]));

                // Anguish: 6 + 6 * 8 = 54 damage.
                int hpBeforeAnguish = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<Anguish>(player, PileType.Hand), enemy);
                Assert.Equal(54, hpBeforeAnguish - enemy.CurrentHp);

                // Ennui + Ecstasy + Clone preserve _sadnessAmount and SadnessGeneratedThisCombat.
                await room.Engine.PlayCardAsync(player, AddToPile<Ennui>(player, PileType.Hand), null);
                await room.Engine.PlayCardAsync(player, AddToPile<Ecstasy>(player, PileType.Hand), null);
                CombatState clone = state.Clone();
                Player clonedPlayer = Assert.Single(clone.Players);
                Assert.Equal(8, clonedPlayer.PlayerCombatState!.SadnessGeneratedThisCombat);
                Assert.Equal(1, clonedPlayer.Creature.GetPower<EnnuiPower>()?.SadnessAmount);

                await room.Engine.EndPlayerTurnAsync();
                // Turn 2: Ennui BeforeHandDraw generates 1 Sadness (+1 SadnessGeneratedThisCombat) and draws +2,
                // then Ecstasy AfterPlayerTurnStart transforms 1 Sadness into Joy.
                Assert.Equal(9, pcs.SadnessGeneratedThisCombat);
                Assert.Single(pcs.Hand.Cards.OfType<Joy>());

                // v0.114.0 Optimism (new Rare): exhaust every Sadness in hand, draw that many; exhausts itself.
                AddToPile<Sadness>(player, PileType.Hand);
                AddToPile<Sadness>(player, PileType.Hand);
                int sadnessBeforeOptimism = pcs.Hand.Cards.OfType<Sadness>().Count();
                int exhaustBeforeOptimism = pcs.ExhaustPile.Cards.Count;
                int drawnBeforeOptimism = pcs.CardsDrawnThisCombat;
                Optimism optimism = AddToPile<Optimism>(player, PileType.Hand);
                Assert.Equal(CardRarity.Rare, optimism.Rarity);
                await room.Engine.PlayCardAsync(player, optimism, null);
                Assert.Empty(pcs.Hand.Cards.OfType<Sadness>());
                Assert.Equal(sadnessBeforeOptimism, pcs.CardsDrawnThisCombat - drawnBeforeOptimism);
                Assert.Equal(exhaustBeforeOptimism + sadnessBeforeOptimism + 1, pcs.ExhaustPile.Cards.Count);
                Assert.Equal(PileType.Exhaust, optimism.Pile?.Type);
                break;

            case "mythicform_simpleton_timber_lockedin_dislikeyou_multiplayer":
                // DislikeYou amplifies arrow damage by 50%.
                await room.Engine.PlayCardAsync(player, AddToPile<DislikeYou>(player, PileType.Hand), enemy);
                Assert.Equal(50, enemy.GetPower<DislikeYouPower>()?.Amount ?? 0);
                pcs.GainArrow();
                TheShot shot = AddToPile<TheShot>(player, PileType.Hand);
                int hpBeforeShot = enemy.CurrentHp;
                await room.Engine.PlayCardAsync(player, shot, enemy);
                // v0.114.0 TheShot 20 x 1.5 = 30.
                Assert.Equal(30, hpBeforeShot - enemy.CurrentHp);

                // Timber triggers 30 AoE only when initial hit kills target.
                Creature enemy2 = state.Enemies[1];
                int enemy2BeforeNonKill = enemy2.CurrentHp;
                await room.Engine.PlayCardAsync(player, AddToPile<Timber>(player, PileType.Hand), enemy);
                Assert.Equal(enemy2BeforeNonKill, enemy2.CurrentHp);

                enemy.SetCurrentHpInternal(20);
                await room.Engine.PlayCardAsync(player, AddToPile<Timber>(player, PileType.Hand), enemy);
                Assert.True(enemy.IsDead);
                Assert.Equal(30, enemy2BeforeNonKill - enemy2.CurrentHp);

                // LockedIn picks a Shoot card from Draw to Hand.
                QuickShot drawShot = AddToPile<QuickShot>(player, PileType.Draw);
                state.CardSelectionSource = new SpecificCardSelector(drawShot);
                await room.Engine.PlayCardAsync(player, AddToPile<LockedIn>(player, PileType.Hand), null);
                Assert.Contains(drawShot, pcs.Hand.Cards);
                // v0.114.0 BlotOutTheSky always draws from FoolCardPool: a Silent owner now gets a free Fool Shoot card.
                HashSet<CardModel> handBeforeBlot = pcs.Hand.Cards.ToHashSet();
                BlotOutTheSky blotCard = AddToPile<BlotOutTheSky>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, blotCard, null);
                CardModel generatedShot = Assert.Single(pcs.Hand.Cards, c => !handBeforeBlot.Contains(c));
                Assert.Contains(CardTag.Shoot, generatedShot.Tags);
                Assert.Contains(FoolCardPool.Instance.AllCards, c => c.Id == generatedShot.Id);
                Assert.Equal(0, generatedShot.EnergyCost);
                Assert.Contains(blotCard, pcs.ExhaustPile.Cards);

                // v0.114.0 MythicForm: Ethereal (removed on upgrade), may be generated in combat, no upgraded-Tarot count;
                // generates 1 un-upgraded Tarot card into Hand on Turn 2.
                MythicForm mythic = AddToPile<MythicForm>(player, PileType.Hand);
                Assert.True(mythic.CanBeGeneratedInCombat);
                Assert.Contains(CardKeyword.Ethereal, mythic.Keywords);
                mythic.Upgrade();
                Assert.DoesNotContain(CardKeyword.Ethereal, mythic.Keywords);
                await room.Engine.PlayCardAsync(player, mythic, null);
                CombatState mythicClone = state.Clone();
                Assert.Equal(1, Assert.Single(mythicClone.Players).Creature.GetPower<MythicFormPower>()?.Amount);
                // Regression (#929 SimFuzz): a cloned combat runs on CombatRunStateSnapshot, not Runs.RunState;
                // BeforeHandDraw must not hard-cast player.RunState and must still generate a Tarot card.
                Player mythicClonePlayer = Assert.Single(mythicClone.Players);
                int cloneHandBeforeDraw = mythicClonePlayer.PlayerCombatState!.Hand.Cards.Count;
                await mythicClonePlayer.Creature.GetPower<MythicFormPower>()!.BeforeHandDraw(mythicClonePlayer);
                Assert.Equal(cloneHandBeforeDraw + 1, mythicClonePlayer.PlayerCombatState.Hand.Cards.Count);

                // Simpleton autoplays lowest-cost playable card (DefendFool 1-cost over Timber 3-cost) before turn end.
                foreach (CardModel c in pcs.Hand.Cards.ToList())
                    CardPileCmd.Remove(c);
                DefendFool lowCost = AddToPile<DefendFool>(player, PileType.Hand);
                Timber highCostTimber = AddToPile<Timber>(player, PileType.Hand);
                await room.Engine.PlayCardAsync(player, AddToPile<Simpleton>(player, PileType.Hand), null);
                await PowerCmd.Apply<BlurPower>(state, player.Creature, 1m, player.Creature, null);
                await PowerCmd.Apply<DexterityPower>(state, player.Creature, 5m, player.Creature, null);
                for (int i = 0; i < 6; i++)
                    AddToPile<StrikeFool>(player, PileType.Draw);

                int hpBeforeTurnEnd = player.Creature.CurrentHp;
                int blockBeforeTurnEnd = player.Creature.Block;
                int enemy2HpBeforeTurnEnd = enemy2.CurrentHp;
                int playedBeforeTurnEnd = pcs.CardsPlayedThisCombat;
                await room.Engine.EndPlayerTurnAsync();
                Assert.Equal(hpBeforeTurnEnd, player.Creature.CurrentHp);
                Assert.Equal(blockBeforeTurnEnd + 2, player.Creature.Block);
                Assert.Equal(enemy2HpBeforeTurnEnd, enemy2.CurrentHp);
                Assert.Equal(playedBeforeTurnEnd + 1, pcs.CardsPlayedThisCombat);
                Assert.Contains(lowCost, pcs.DiscardPile.Cards);
                Assert.Contains(highCostTimber, pcs.DiscardPile.Cards);
                // Turn 2 BeforeHandDraw generated 1 (un-upgraded since v0.114.0) Tarot card before drawing 5 StrikeFools.
                Assert.False(Assert.Single(pcs.Hand.Cards, c => TarotCards.All.Any(t => t.Id == c.Id)).IsUpgraded);

                Assert.True(ModelDb.Card<Compost>().IsMultiplayerOnly);
                Assert.True(ModelDb.Card<Enrapture>().IsMultiplayerOnly);
                Assert.True(ModelDb.Card<SelfDeprecate>().IsMultiplayerOnly);
                break;
        }
    }

    private static T AddToPile<T>(Player player, PileType pile) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }

    private sealed class SpecificCardSelector(CardModel chosen) : ICardSelectionDecisionSource
    {
        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
            Task.FromResult<IReadOnlyList<CardModel>>(
                request.Candidates.Contains(chosen) ? [chosen] : request.Candidates.Take(request.MinCount).ToArray());
    }
}
