using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.FoolArrows;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.CardPools;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Events;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Characters;

[Collection("ModelDb")]
public sealed class FoolCrossColorTests : IDisposable
{
    public FoolCrossColorTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("fool_card_pool_and_status_order")]
    [InlineData("fool_starting_setup_and_archaic_tooth")]
    public async Task FoolCardPoolAndStarterSetup_MatchSource(string scenario)
    {
        switch (scenario)
        {
            case "fool_card_pool_and_status_order":
            {
                Type[] expectedPool =
                [
                    typeof(AllIsWell), typeof(Anguish), typeof(Archery), typeof(ArrowStab),
                    typeof(BlossomShot), typeof(BlotOutTheSky), typeof(Bounce), typeof(BowSmack),
                    typeof(Brace), typeof(BrightenUp), typeof(ChoBarbs), typeof(ChoChorus),
                    typeof(ChoFirm), typeof(ChoKi), typeof(ChoLimbs), typeof(ChoReady),
                    typeof(Coda), typeof(Compost), typeof(CoveringFire), typeof(DeepStretch),
                    typeof(DefendFool), typeof(DislikeYou), typeof(DivineShot), typeof(Ecstasy),
                    typeof(Encore), typeof(Ennui), typeof(Enrapture), typeof(Ensemble),
                    typeof(EpicRetrieval), typeof(ExplosiveArrows), typeof(Florescence), typeof(Growth),
                    typeof(HarmonicShield), typeof(HeavyShot), typeof(HiddenRoots), typeof(HobblingShot),
                    typeof(Honing), typeof(Jest), typeof(KiChant), typeof(KiHop),
                    typeof(KiRest), typeof(KiVerse), typeof(KiWhip), typeof(LockedIn),
                    typeof(FoolishShot), typeof(LostLute), typeof(Lullaby), typeof(MelodicShot),
                    typeof(Motif), typeof(MourningBlow), typeof(MythicForm), typeof(Nail),
                    typeof(Nock), typeof(PunchOfJoy), typeof(QuickShot), typeof(Quietus),
                    typeof(Quiver), typeof(RainShot), typeof(RapidShot), typeof(Recall),
                    typeof(RepeatShot), typeof(RhythmStrike), typeof(Rummage), typeof(SelfDeprecate),
                    typeof(SerratedArrows), typeof(ShowOff), typeof(Simpleton), typeof(SkyShot),
                    typeof(SongPath), typeof(SorrowfulStrike), typeof(SteadyCadence), typeof(StrikeFool),
                    typeof(Timber), typeof(TrickShot), typeof(Tune), typeof(Uproot),
                    typeof(ViolentChord), typeof(Vitriol), typeof(WahBlast), typeof(WahCho),
                    typeof(WahChoKi), typeof(WahHammer), typeof(WahKi), typeof(WahSymphony),
                    typeof(WahSweep), typeof(WahTouch), typeof(WhistlingArrows), typeof(Wrest),
                    typeof(Yah), typeof(Yoga),
                ];
                Type[] multiplayerOnly =
                [
                    typeof(Compost), typeof(CoveringFire), typeof(Enrapture),
                    typeof(Ensemble), typeof(SelfDeprecate), typeof(Yoga),
                ];
                Fool fool = ModelDb.Character<Fool>();
                IReadOnlyList<CardModel> actual = fool.CardPool.AllCards;
                Assert.Equal(90, expectedPool.Length);
                Assert.Equal(expectedPool, actual.Select(card => card.GetType()));
                Assert.Equal(90, actual.Select(card => card.Id).Distinct().Count());
                Assert.Equal(multiplayerOnly, actual.Where(c => c.IsMultiplayerOnly).Select(c => c.GetType()));

                PlayerUnlockState allUnlocked = PlayerUnlockState.AllUnlocked();
                Assert.Equal(84, fool.CardPool.GetUnlockedCards(allUnlocked, isMultiplayer: false).Count());
                Assert.Equal(expectedPool.Where(t => !multiplayerOnly.Contains(t)),
                    fool.CardPool.GetUnlockedCards(allUnlocked, isMultiplayer: false).Select(c => c.GetType()));

                (string Epoch, Type[] Gated)[] epochs =
                [
                    (FoolCardPool.Fool2EpochId, [typeof(ArrowStab), typeof(Quiver), typeof(BlossomShot)]),
                    (FoolCardPool.Fool5EpochId, [typeof(WahTouch), typeof(KiVerse), typeof(ChoLimbs)]),
                    (FoolCardPool.Fool7EpochId, [typeof(SorrowfulStrike), typeof(Ennui), typeof(Growth)]),
                ];
                foreach ((string epoch, Type[] gated) in epochs)
                {
                    string[] others = epochs.Select(e => e.Epoch).Where(id => id != epoch).ToArray();
                    var partial = new PlayerUnlockState([], others);
                    Assert.Equal(expectedPool.Where(t => !gated.Contains(t)),
                        fool.CardPool.GetUnlockedCards(partial, isMultiplayer: true).Select(c => c.GetType()));
                }

                Assert.Equal(
                    [typeof(Ironclad), typeof(Silent), typeof(Regent), typeof(Necrobinder), typeof(Defect), typeof(Fool)],
                    ModelDb.AllCharacters.Select(character => character.GetType()));

                Type[] expectedStatus =
                [
                    typeof(Beckon), typeof(Burn), typeof(Dazed), typeof(Debris),
                    typeof(FranticEscape), typeof(Infection), typeof(Wither), typeof(Sadness),
                    typeof(Slimed), typeof(Soot), typeof(Toxic), typeof(Sts2Sim.Core.Models.Cards.Void), typeof(Wound),
                ];
                Assert.Equal(expectedStatus, StatusCardPool.Instance.AllCards.Select(c => c.GetType()));
                break;
            }
            case "fool_starting_setup_and_archaic_tooth":
            {
                var run = new RunState("fool-g6-starter", new Overgrowth());
                Player player = Player.CreateForNewRun(ModelDb.Character<Fool>(), run);
                run.AddPlayer(player);

                Assert.Equal(77, player.Creature.CurrentHp);
                Assert.Equal(77, player.Creature.MaxHp);
                Assert.Equal(99, player.Gold);
                Assert.Equal(3, player.MaxEnergy);
                Assert.Equal(1, player.Character.BaseArrowSlotCount);
                Type[] expectedDeck =
                [
                    typeof(StrikeFool), typeof(StrikeFool), typeof(StrikeFool), typeof(StrikeFool), typeof(StrikeFool),
                    typeof(DefendFool), typeof(DefendFool), typeof(DefendFool), typeof(DefendFool),
                    typeof(FoolishShot),
                ];
                Assert.Equal(expectedDeck, player.Deck.Cards.Select(c => c.GetType()));
                Assert.IsType<SongBow>(Assert.Single(player.Relics));

                var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
                await room.Enter(run);
                Assert.Equal(1, player.PlayerCombatState!.AvailableArrowCount);
                Assert.Equal(6, player.Creature.GetPower<AimPower>()?.Amount ?? 0);

                // Orobas offers ArchaicTooth when Fool carries FoolishShot without SongBow; obtaining ArchaicTooth transforms FoolishShot -> DivineShot.
                var toothRun = new RunState("fool-g6-archaic", new Overgrowth());
                Player toothPlayer = Player.CreateForNewRun(ModelDb.Character<Fool>(), toothRun);
                toothRun.AddPlayer(toothPlayer);
                FoolishShot starterShot = toothPlayer.Deck.Cards.OfType<FoolishShot>().Single();
                starterShot.Upgrade();
                await CardCmd.Enchant<Sharp>(starterShot, 2m);
                toothPlayer.RemoveRelicInternal(toothPlayer.Relics.OfType<SongBow>().Single());
                var orobas = (Orobas)ModelDb.Event<Orobas>().MutableClone();
                orobas.AssignOwner(toothPlayer);
                orobas.BeginEvent(toothRun);
                Assert.Equal(nameof(ArchaicTooth), orobas.CurrentOptions[2].Key);
                await RelicCmd.Obtain(ModelDb.Relic<ArchaicTooth>(), toothPlayer);
                DivineShot upgradedShot = Assert.Single(toothPlayer.Deck.Cards.OfType<DivineShot>());
                Assert.Empty(toothPlayer.Deck.Cards.OfType<FoolishShot>());
                Assert.True(upgradedShot.IsUpgraded);
                Assert.IsType<Sharp>(Assert.Single(upgradedShot.Enchantments));
                break;
            }
        }
    }

    [Theory]
    [InlineData("silent_prismatic_kaleidoscope_splash_orobas_colorful")]
    [InlineData("silent_shoot_card_auto_arrow_slot")]
    public async Task SilentCrossColorEntrances_IncludeFoolAndAutoOpenArrowSlot(string scenario)
    {
        switch (scenario)
        {
            case "silent_prismatic_kaleidoscope_splash_orobas_colorful":
            {
                HashSet<ModelId> foolIds = ModelDb.Character<Fool>().CardPool.AllCards.Select(c => c.Id).ToHashSet();

                // PrismaticGem adds FoolCardPool to Silent card rewards.
                var gemRun = new RunState("fool-g6-prismatic", new Overgrowth());
                Player gemPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), gemRun);
                gemRun.AddPlayer(gemPlayer);
                await RelicCmd.Obtain(ModelDb.Relic<PrismaticGem>(), gemPlayer);
                CardCreationOptions modified = Hook.ModifyCardRewardCreationOptions(gemRun, gemPlayer,
                    new CardCreationOptions([gemPlayer.Character.CardPool], CardCreationSource.Encounter, CardRarityOddsType.RegularEncounter)
                        .WithFlags(CardCreationFlags.IsCardReward));
                Assert.Contains(modified.CardPools, p => p is FoolCardPool);
                // Native order: UnlockState.CharacterCardPools (= ModelDb.AllCharacters order) unioned with the
                // original pools, so Fool is last and Silent keeps its own slot.
                Assert.Equal(
                    "IroncladCardPool,SilentCardPool,RegentCardPool,NecrobinderCardPool,DefectCardPool,FoolCardPool",
                    string.Join(",", modified.CardPools.Select(p => p.GetType().Name)));

                // Kaleidoscope offers cards drawn from other pools including FoolCardPool.
                bool observedKaleidoscopeFool = false;
                for (int seed = 0; seed < 32 && !observedKaleidoscopeFool; seed++)
                {
                    var kRun = new RunState($"fool-g6-kaleido-{seed}", new Overgrowth());
                    Player kPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), kRun);
                    kRun.AddPlayer(kPlayer);
                    var eventRoom = new EventRoom(() => (AbyssalBaths)ModelDb.Event<AbyssalBaths>().MutableClone());
                    kRun.PushRoom(eventRoom);
                    await eventRoom.Enter(kRun);
                    int nicheBefore = kPlayer.PlayerRng.NonCombatGeneration.Counter;
                    await RelicCmd.Obtain(ModelDb.Relic<Kaleidoscope>(), kPlayer);
                    Assert.Equal(2, kPlayer.PlayerRng.NonCombatGeneration.Counter - nicheBefore);
                    Assert.True(eventRoom.Event.TryDequeuePendingRewardOffer(out RewardsSet? rewards));
                    CardReward second = Assert.IsType<CardReward>(Assert.Single(rewards!.ExtraRewards));
                    if (rewards.Card.Options.Concat(second.Options).Any(c => foolIds.Contains(c.Id)))
                        observedKaleidoscopeFool = true;
                }
                Assert.True(observedKaleidoscopeFool);

                // Exact results for pinned seeds lock the StableShuffle input order (entry-slug Ordinal sort of the
                // five non-Silent pools: DEFECT, FOOL, IRONCLAD, NECROBINDER, REGENT) followed by two shuffles.
                var kaleidoscopeSignatures = new List<string>();
                for (int seed = 0; seed < 4; seed++)
                {
                    var kRun = new RunState($"fool-g6-kaleido-{seed}", new Overgrowth());
                    Player kPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), kRun);
                    kRun.AddPlayer(kPlayer);
                    var eventRoom = new EventRoom(() => (AbyssalBaths)ModelDb.Event<AbyssalBaths>().MutableClone());
                    kRun.PushRoom(eventRoom);
                    await eventRoom.Enter(kRun);
                    await RelicCmd.Obtain(ModelDb.Relic<Kaleidoscope>(), kPlayer);
                    Assert.True(eventRoom.Event.TryDequeuePendingRewardOffer(out RewardsSet? pinned));
                    CardReward pinnedSecond = Assert.IsType<CardReward>(Assert.Single(pinned!.ExtraRewards));
                    kaleidoscopeSignatures.Add(string.Join("+", new[] { pinned.Card, pinnedSecond }.Select(r =>
                        string.Join("/", r.Options.Select(c => PoolOwnerEntry(c))))));
                }
                Assert.Equal("DEFECT/NECROBINDER/IRONCLAD+REGENT/NECROBINDER/IRONCLAD | REGENT/NECROBINDER/FOOL+NECROBINDER/REGENT/FOOL | REGENT/FOOL/IRONCLAD+DEFECT/REGENT/IRONCLAD | IRONCLAD/REGENT/FOOL+DEFECT/IRONCLAD/FOOL", string.Join(" | ", kaleidoscopeSignatures));

                // Splash draws other-character Attacks including Fool Attacks.
                bool observedSplashFool = false;
                for (int seed = 0; seed < 32 && !observedSplashFool; seed++)
                {
                    var sRun = new RunState($"fool-g6-splash-{seed}", new Overgrowth());
                    Player sPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), sRun);
                    sRun.AddPlayer(sPlayer);
                    var combat = new CombatRoom(() => (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone());
                    await combat.Enter(sRun);
                    combat.Engine.State.CardSelectionSource = new PreferIdCardSelector(foolIds);
                    sPlayer.PlayerCombatState!.Energy = 10;
                    await combat.Engine.PlayCardAsync(sPlayer, AddToPile<Splash>(sPlayer, PileType.Hand), null);
                    if (sPlayer.PlayerCombatState.Hand.Cards.Any(c => foolIds.Contains(c.Id)))
                        observedSplashFool = true;
                }
                Assert.True(observedSplashFool);

                // Exact generated candidates for pinned seeds: candidate pools are CharacterCardPools minus Silent
                // in native character order (Ironclad, Regent, Necrobinder, Defect, Fool).
                var splashSignatures = new List<string>();
                for (int seed = 0; seed < 4; seed++)
                {
                    var sRun = new RunState($"fool-g6-splash-{seed}", new Overgrowth());
                    Player sPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), sRun);
                    sRun.AddPlayer(sPlayer);
                    var combat = new CombatRoom(() => (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone());
                    await combat.Enter(sRun);
                    var capture = new PreferIdCardSelector(foolIds);
                    combat.Engine.State.CardSelectionSource = capture;
                    sPlayer.PlayerCombatState!.Energy = 10;
                    await combat.Engine.PlayCardAsync(sPlayer, AddToPile<Splash>(sPlayer, PileType.Hand), null);
                    splashSignatures.Add(string.Join("/", capture.LastCandidates.Select(c => c.Id.Entry)));
                }
                Assert.Equal("WAH_BLAST/RAPID_SHOT/ASTRAL_PULSE | ROCKET_PUNCH/DEFILE/CINDER | ANGER/GO_FOR_THE_EYES/ARROW_STAB | SKY_SHOT/VEILPIERCER/METEOR_STRIKE", string.Join(" | ", splashSignatures));

                // Orobas SeaGlass enumerates all 6 playable characters including Fool and can select FoolCardPool.
                var orobasCanonical = (Orobas)ModelDb.Event<Orobas>().MutableClone();
                Assert.Equal(6, orobasCanonical.AllPossibleOptions.OfType<SeaGlass>().Count());
                Assert.Contains(orobasCanonical.AllPossibleOptions.OfType<SeaGlass>(), sg => sg.CharacterId == ModelDb.Character<Fool>().Id);
                Assert.Equal(
                    ["IRONCLAD", "SILENT", "REGENT", "NECROBINDER", "DEFECT", "FOOL"],
                    orobasCanonical.AllPossibleOptions.OfType<SeaGlass>().Select(sg => sg.CharacterId.Entry));
                // NextItem(otherCharacters) draws from the native order minus the owner; pinned seeds lock the result.
                var orobasSeaGlass = new List<string>();
                for (int seed = 0; seed < 60; seed++)
                {
                    var oRun = new RunState($"fool-g6-orobas-{seed}", new Overgrowth());
                    Player oPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), oRun);
                    oRun.AddPlayer(oPlayer);
                    var oEvent = (Orobas)ModelDb.Event<Orobas>().MutableClone();
                    oEvent.AssignOwner(oPlayer);
                    oEvent.BeginEvent(oRun);
                    EventOption? glass = oEvent.CurrentOptions.FirstOrDefault(o => o.Key == nameof(SeaGlass));
                    if (glass?.OfferedContent?.Values.FirstOrDefault(v => v.Name == "CharacterId")?.Value is { } glassCharacter)
                        orobasSeaGlass.Add($"{seed}:{glassCharacter}");
                }
                Assert.Equal("5:NECROBINDER,11:NECROBINDER,12:IRONCLAD,15:IRONCLAD,17:NECROBINDER,19:DEFECT,20:IRONCLAD,26:FOOL,27:FOOL,35:DEFECT,53:DEFECT,54:REGENT", string.Join(",", orobasSeaGlass));
                var seaGlassRun = new RunState("fool-g6-seaglass-fool", new Overgrowth());
                Player sgPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), seaGlassRun);
                seaGlassRun.AddPlayer(sgPlayer);
                int sgDeckBefore = sgPlayer.Deck.Cards.Count;
                seaGlassRun.ConfigureCardSelectionSource(new PreferIdCardSelector(foolIds));
                var seaGlass = (SeaGlass)ModelDb.Relic<SeaGlass>().MutableClone();
                seaGlass.CharacterId = ModelDb.Character<Fool>().Id;
                await RelicCmd.Obtain(seaGlass, sgPlayer);
                Assert.Equal(sgDeckBefore + 1, sgPlayer.Deck.Cards.Count);
                Assert.Contains(sgPlayer.Deck.Cards.Skip(sgDeckBefore), c => foolIds.Contains(c.Id));

                // ColorfulPhilosophers: IsAllowed counts only non-Fool character pools (> 1 required).
                ModelDb.ResetForTests();
                ModelDb.Init(ContentRegistry.AllTypes.Where(t =>
                    !typeof(CharacterModel).IsAssignableFrom(t) || t == typeof(Ironclad) || t == typeof(Fool)));
                var twoCharRun = new RunState("fool-g6-colorful-gate", new Overgrowth());
                Player ironcladOnly = Player.CreateForNewRun(ModelDb.Character<Ironclad>(), twoCharRun);
                twoCharRun.AddPlayer(ironcladOnly);
                Assert.False(ModelDb.Event<ColorfulPhilosophers>().IsAllowed(twoCharRun));

                // Full roster: 6 characters -> IsAllowed true, Fool eligible as option in native order ("FOOL" between "REGENT" and "SILENT").
                ModelDb.ResetForTests();
                ModelDb.Init(ContentRegistry.AllTypes);
                ColorfulPhilosophers? foolOfferEvent = null;
                for (int seed = 0; seed < 32 && foolOfferEvent is null; seed++)
                {
                    var cRun = new RunState($"fool-g6-colorful-offer-{seed}", new Overgrowth());
                    Player cPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), cRun);
                    cRun.AddPlayer(cPlayer);
                    var ev = (ColorfulPhilosophers)ModelDb.Event<ColorfulPhilosophers>().MutableClone();
                    Assert.True(ev.IsAllowed(cRun));
                    ev.AssignOwner(cPlayer);
                    ev.BeginEvent(cRun);
                    if (ev.CurrentOptions.Any(o => o.Key == "FOOL"))
                        foolOfferEvent = ev;
                }
                Assert.NotNull(foolOfferEvent);
                var colorfulKeys = new List<string>();
                for (int seed = 0; seed < 6; seed++)
                {
                    var pRun = new RunState($"fool-g6-colorful-offer-{seed}", new Overgrowth());
                    Player pPlayer = Player.CreateForNewRun(ModelDb.Character<Silent>(), pRun);
                    pRun.AddPlayer(pPlayer);
                    var pEvent = (ColorfulPhilosophers)ModelDb.Event<ColorfulPhilosophers>().MutableClone();
                    pEvent.AssignOwner(pPlayer);
                    pEvent.BeginEvent(pRun);
                    colorfulKeys.Add(string.Join("/", pEvent.CurrentOptions.Select(o => o.Key)));
                }
                Assert.Equal("REGENT/FOOL/DEFECT | IRONCLAD/REGENT/DEFECT | REGENT/FOOL/DEFECT | IRONCLAD/FOOL/DEFECT | NECROBINDER/IRONCLAD/REGENT | NECROBINDER/FOOL/DEFECT", string.Join(" | ", colorfulKeys));
                await foolOfferEvent!.ChooseOption(foolOfferEvent.CurrentOptions.Single(o => o.Key == "FOOL"));
                var foolRewards = new List<RewardsSet>();
                while (foolOfferEvent.TryDequeuePendingRewardOffer(out RewardsSet? set))
                    foolRewards.Add(set);
                Assert.Equal(3, foolRewards.Count);
                Assert.All(foolRewards, set => Assert.All(set.Card.Options, card => Assert.Contains(card.Id, foolIds)));
                break;
            }
            case "silent_shoot_card_auto_arrow_slot":
            {
                var run = new RunState("fool-g6-silent-shoot", new Overgrowth());
                Player silent = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
                run.AddPlayer(silent);
                var room = new CombatRoom(() => (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone());
                await room.Enter(run);
                CombatState state = room.Engine.State;
                Creature enemy = Assert.Single(state.Enemies);
                enemy.SetMaxHpInternal(100);
                enemy.SetCurrentHpInternal(100);
                PlayerCombatState pcs = silent.PlayerCombatState!;
                Assert.Empty(pcs.Arrows);

                var quickShot = (QuickShot)ModelDb.Card<QuickShot>().MutableClone();
                quickShot.AssignOwner(silent);
                await CardPileCmd.Generate(state, quickShot, PileType.Hand);
                ArrowSlot slot = Assert.Single(pcs.Arrows);
                Assert.Equal(ArrowStatus.Retrieved, slot.Status);
                Assert.Equal(1, pcs.AvailableArrowCount);

                await room.Engine.PlayCardAsync(silent, quickShot, enemy);
                Assert.Equal(90, enemy.CurrentHp);
                Assert.Equal(ArrowStatus.Away, slot.Status);
                Assert.Equal(1, enemy.GetPower<ImpaledPower>()?.Amount ?? 0);
                break;
            }
        }
    }

    private static T AddToPile<T>(Player player, PileType pile) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile);
        return card;
    }

    private static string PoolOwnerEntry(CardModel card) =>
        ModelDb.AllCharacters.Single(character => character.CardPool.AllCards.Any(c => c.Id == card.Id)).Id.Entry;

    private sealed class PreferIdCardSelector(HashSet<ModelId> preferredIds) : ICardSelectionDecisionSource
    {
        public IReadOnlyList<CardModel> LastCandidates { get; private set; } = [];

        public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
        {
            LastCandidates = request.Candidates.ToArray();
            CardModel? match = request.Candidates.FirstOrDefault(c => preferredIds.Contains(c.Id));
            return Task.FromResult<IReadOnlyList<CardModel>>(
                match is not null ? [match] : request.Candidates.Take(request.MinCount).ToArray());
        }
    }
}
