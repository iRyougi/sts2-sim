using Sts2Sim.Core.Models;

namespace Sts2Sim.Core.Combat.StateDescription;

/// <summary>
/// Explicit fail-closed registry used by the audit test. Production key construction never uses
/// reflection; contributors append their own private future-behavior state.
/// </summary>
internal static class CombatPrivateStateRegistry
{
    private static readonly HashSet<string> ContributorFields =
    [
        "VelvetChoker._cardsPlayedThisTurn", "DustyTome._ancientCard",
        "Girya._timesLifted", "LastingCandy._combatRewardsSeen",
        "Momentum._extraDamage",
        "CacophonyPower._cardsLeft", "TheScythe._increasedDamage",
        "Fetch._finishedRound", "Fetch._finishedSide", "Fetch._finishedTurnNumbers",
        "OblivionPower._amountsForPlayedCards",
        "FishingRod.<CombatsSeen>k__BackingField", "LavaRock.<HasTriggered>k__BackingField",
        "GoldenCompass.<GoldenPathAct>k__BackingField", "ToyBox.<CombatsSeen>k__BackingField",
        "FurCoat._actIndex", "FurCoat._markedCoordinates",
        "BrilliantScarf._cardsPlayed", "IronClub._cardsPlayed", "MusicBox._wasUsedThisTurn", "MusicBox._cardBeingPlayed", "ThrowingAxe._used",
        "PaelsEye._usedThisCombat", "PaelsEye._wasOwnerPartOfLastPlayerTurn", "PaelsEye._eligibleForExtraTurn",
        "PaelsLegion._cooldown", "PaelsLegion._affectedCardPlay", "PaelsTooth._removedCards",
        "PaelsWing._rewardsSacrificed", "PaelsTears._gainNextTurn", "PollinousCore._handsSeen", "PumpkinCandle.<KindleCount>k__BackingField",
        "SilverCrucible.<TimesUsed>k__BackingField", "SilverCrucible.<TreasureRoomsEntered>k__BackingField",
        "WingedBoots.<TimesUsed>k__BackingField",
        "ArtOfWar._attackPlayedThisTurn", "ArtOfWar._previousTurnHadNoAttack",
        "BeatingRemnant._hpLostThisTurn", "BeltBuckle._dexterityApplied",
        "BookOfFiveRings._cardsAdded", "BurningSticks._wasUsedThisCombat",
        "CentennialPuzzle._triggeredThisCombat", "GalacticDust._starsSpent",
        "HappyFlower._turnCounter", "JossPaper._cardsExhausted", "JossPaper._etherealCount",
        "Kunai._attacksThisTurn", "Kusarigama._attacksThisTurn", "LavaLamp._tookDamageThisCombat",
        "LetterOpener._skillsThisTurn", "LizardTail._wasUsed", "MiniRegent._triggeredThisTurn",
        "Nunchaku._attacksPlayed", "Orichalcum._shouldTrigger",
        "OrnamentalFan._attacksThisTurn",
        "Pendulum._turnCounter", "PenNib._attacksPlayed", "Permafrost._triggeredThisCombat",
        "Pocketwatch._cardsPlayedThisTurn", "Pocketwatch._shouldDrawExtra",
        "RainbowRing._playedAttack", "RainbowRing._playedSkill", "RainbowRing._playedPower",
        "RainbowRing._triggeredThisTurn", "RippleBasin._attackPlayedThisTurn",
        "Regalite._usedThisTurn",
        "Shuriken._attacksThisTurn", "SilkenTress._isUsed",
        "StoneCalendar._shouldTriggerThisTurn", "SwordOfStone._eliteVictories",
        "Toolbox._wasUsedThisCombat", "TuningFork._skillsPlayed",
        "UnsettlingLamp._triggeringCard", "UnsettlingLamp._finished",
        "Vambrace._triggeredThisCombat", "VenerableTeaSet._isArmed",
        "BoneTea._combatsLeft", "EmberTea._combatsLeft", "TeaOfDiscourtesy._combatsLeft",
        "HistoryCourse._lastAttack", "HistoryCourse._lastAttackTurn",
        "FakeHappyFlower._turnsSeen", "FakeOrichalcum._shouldTrigger",
        "FakeVenerableTeaSet._isArmed",
        "WongosMysteryTicket._combatsFinished", "WongosMysteryTicket._gaveRelics",
        "AutomationPower._cardsLeft", "IllusionPower._followUpStateId", "IllusionPower._isReviving",
        "OrbitPower._energySpent",
        "BattlewornDummyTimeLimitPower._shouldEscape",
        "PaleBlueDotPower._alreadyActivatedThisTurn",
        "PanachePower._alreadyApplied", "PanachePower._cardsLeft",
        "MonologuePower._strengthApplied",
        "MonologuePower._strength",
        "Monologue._power",
        "RitualPower._skipFirstEnemyTurnEnd", "SlowPower._cardsPlayedSinceOwnerTurnStart",
        "SwordSagePower._grantedReplays",
        "ReattachPower._isReviving", "TenderPower._cardsPlayedThisTurn",
        "TheBombPower._damage", "TheBall._damage", "VoidFormPower._cardsPlayedThisTurn",
        "ToricToughnessPower.<StoredBlock>k__BackingField",
        "Abundance._generatedCandidates", "Bolas._playedOnTurnNumber",
        "Maul._damage", "Maul._increase", "Maul._extraDamageFromMaulPlays",
        "Whistle._damage",
        "Bombardment._hasReplayed", "Dowsing._unknownRoomsEntered",
        "SpoilsMap.<SpoilsActIndex>k__BackingField", "SpoilsMap.<SpoilsCoord>k__BackingField",
        "Guilty._combatsCompleted", "TheHunt._pendingCardRewards", "ThrummingHatchet._playedOnTurnNumber",
        "MadScience.<Rider>k__BackingField",
        "NightmarePower._hasSelectedCard", "NightmarePower._selectedCardId",
        "NightmarePower._selectedCardUpgradeLevel",
        "HardenedShellPower._damageReceivedThisTurn", "HeistPower.<Target>k__BackingField",
        "SkittishPower._hasGainedBlockThisTurn", "SmoggyPower._skillCardPlayStartedThisTurn",
        "ThieveryPower._goldStolen", "ThieveryPower.<Target>k__BackingField",
        "CurlUpPower._triggeringCard", "SandpitPower.<Target>k__BackingField",
        "SlothPower._cardsPlayed", "SurroundedPower.<Facing>k__BackingField",
        "SwipePower.<StolenCard>k__BackingField", "Disintegration.<PowerAmount>k__BackingField",
        "BowlbugRock._isOffBalance", "KnowledgeDemon._curseCounter",
        "Axebot._stockAmount", "Fabricator._lastSpawned", "FrogKnight._charged",
        "DampenPower._casters", "DampenPower._downgraded",
        "PossessSpeedPower._stolen", "PossessStrengthPower._stolen",
        "AdaptablePower._isReviving", "ChainsOfBindingPower._cardsAfflictedThisTurn",
        "ChainsOfBindingPower._boundCardPlayedThisTurn", "NemesisPower._shouldApplyIntangible",
        "WitheringPresencePower._cardsPlayed", "WitheringPresencePower.<Target>k__BackingField",
        "Aeonglass._additionalStrength",
        "Aeonglass._witherUpgradeCount", "Queen._hasAmalgamDied", "Queen._amalgam",
        "CeremonialBeast._beastCryState",
        "ToughEgg._isHatched", "ToughEgg._afterHatchedState",
        "TestSubject._respawns", "TestSubject._extraMultiClawCount",
        "Wither._fakeUpgradeLevel",
        "MawBank._hasItemBeenBought",
        "CorpseSlug._isRavenous", "CorpseSlug._starterMoveIdx", "GasBomb._hasExploded",
        "LagavulinMatriarch._isAwake", "LagavulinMatriarch._isShellAwake",
        "LivingFog._bloatAmount", "PhantasmalGardener._enlargeTriggers",
        "SoulFysh._isInvisible", "Toadpole._isFront",
        "TwoTailedRat._starterMoveIndex", "TwoTailedRat._turnsUntilSummonable",
        "TwoTailedRat._callForBackupCount",
        "WaterfallGiant._currentPressureGunDamage", "WaterfallGiant._steamEruptionDamage",
        "DemonTongue._triggeredThisTurn", "RedSkull._strengthApplied", "RuinedHelmet._usedThisCombat",
        "CrimsonMantlePower._selfDamage", "DarkEmbracePower._etherealExhausts",
        "HellraiserPower._infiniteAutoPlaysThisTurn", "InfernoPower._selfDamage",
        "JugglingPower._attacksPlayedThisTurn", "RupturePower._pendingStrength",
        "Rampage._damage", "Thrash._damage",
        "Metronome._orbsChanneled", "FeralPower._zeroCostAttacksPlayed",
        "ImitationLearningPower._playerTarget", "ImitationLearningPower._pending",
        "Claw._damage", "Claw._increase",
        "GeneticAlgorithm._currentBlock", "GeneticAlgorithm._increasedBlock",
        "GeneticAlgorithm._increase",
    ];

    private static readonly IReadOnlyDictionary<string, string> IgnoredFields =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TouchOfOrobas._starterRelic"] =
                "Pickup replacement has already happened; this saved ID has no future combat read.",
            ["TouchOfOrobas._upgradedRelic"] =
                "Pickup replacement has already happened; this saved ID has no future combat read.",
            ["SeaGlass._characterId"] =
                "The character choice is used only during the already-completed pickup effect and for presentation.",
            ["CeremonialBeast._isStunnedByPlowRemoval"] =
                "Native hurt-animation flag; its presentation reader is omitted in the headless simulator.",
            ["CeremonialBeast._isInSecondPhase"] =
                "Native background-animation flag; its presentation reader is omitted in the headless simulator.",
            ["Chomper.<ScreamFirst>k__BackingField"] =
                "Initial configuration already materialized into the fingerprinted move-state machine.",
            ["Inklet._middleInklet"] =
                "Initial configuration already materialized into the fingerprinted move-state machine.",
            ["KinFollower._startsWithDance"] =
                "Initial configuration already materialized into the fingerprinted move-state machine.",
            ["LouseProgenitor.<Curled>k__BackingField"] =
                "Presentation flag; no simulator behavior reads it after assignment.",
            ["Nibbit._isFront"] =
                "Initial branch input already materialized into the fingerprinted current move.",
            ["Nibbit._isAlone"] =
                "Initial branch input already materialized into the fingerprinted current move.",
            ["ThievingHopper.<IsHovering>k__BackingField"] =
                "Presentation flag; FlutterPower and current move state contain all future behavior.",
            ["Tunneler.<IsStunned>k__BackingField"] =
                "Presentation flag; the transient STUNNED move encodes the future transition.",
            ["Wriggler._startStunned"] =
                "Initial configuration already materialized into the fingerprinted move-state machine.",
            ["ScrollOfBiting.<StarterMoveIdx>k__BackingField"] =
                "Initial rotation is materialized into the fingerprinted current move state.",
            ["TestSubject._deadState"] =
                "Stable reference to RESPAWN_MOVE in the fingerprinted move-state machine.",
            ["PunchConstruct._startsWithFastPunch"] =
                "Initial event configuration is materialized into the fingerprinted starting move.",
            ["PunchConstruct._startingHpReduction"] =
                "Room-entry configuration is materialized into the creature HP before search begins.",
            ["TerrorEel._terrorState"] =
                "Stable reference to TERROR_MOVE in the fingerprinted move-state machine.",
            ["WaterfallGiant._aboutToBlowState"] =
                "Stable reference to ABOUT_TO_BLOW_MOVE in the fingerprinted move-state machine.",
        };

    private static readonly HashSet<string> TransientFields =
    [
        "GigantificationPower._claimedCommand", "GigantificationPower._claimedCard",
        "VigorPower._commandBeingModified", "VigorPower._amountWhenAttackStarted",
        "PenNib._doubleDamagePlay", "Regret._cardsInHand", "BeaconOfHopePower._isSharingBlock",
        "CalamityPower._eligibleCardPlays", "MonologuePower._amountsForPlayedCards",
        "StranglePower._amountsForPlayedCards", "AfterimagePower._amountsForPlayedCards",
        "SerpentFormPower._amountsForPlayedCards",
        "StormPower._amountsForPlayedCards", "SubroutinePower._amountsForPlayedCards",
        "SoulboundPower._isAddingSoul",
    ];

    private static readonly HashSet<string> DerivedFields =
    [
        "Queen._burnBrightForMeState", "Queen._enragedState",
        "Alignment.<Spec>k__BackingField",
        "Arsenal.<Spec>k__BackingField",
        "AstralPulse.<Spec>k__BackingField",
        "BeaconOfHope.<Spec>k__BackingField",
        "BeatIntoShape.<Spec>k__BackingField",
        "BelieveInYou.<Spec>k__BackingField",
        "BigBang.<Spec>k__BackingField",
        "Bulwark.<Spec>k__BackingField",
        "Calamity.<Spec>k__BackingField",
        "CelestialMight.<Spec>k__BackingField",
        "CloakOfStars.<Spec>k__BackingField",
        "Comet.<Spec>k__BackingField",
        "Conqueror.<Spec>k__BackingField",
        "Constellation.<Spec>k__BackingField",
        "Coordinate.<Spec>k__BackingField",
        "CrescentSpear.<Spec>k__BackingField",
        "CrushUnder.<Spec>k__BackingField",
        "DarkShackles.<Spec>k__BackingField",
        "DefendSilent.<Spec>k__BackingField",
        "Devastate.<Spec>k__BackingField",
        "DramaticEntrance.<Spec>k__BackingField",
        "DyingStar.<Spec>k__BackingField",
        "Entropy.<Spec>k__BackingField",
        "EternalArmor.<Spec>k__BackingField",
        "Finesse.<Spec>k__BackingField",
        "Fisticuffs.<Spec>k__BackingField",
        "FlashOfSteel.<Spec>k__BackingField",
        "ForegoneConclusion.<Spec>k__BackingField",
        "Furnace.<Spec>k__BackingField",
        "GammaBlast.<Spec>k__BackingField",
        "GangUp.<Spec>k__BackingField",
        "GatherLight.<Spec>k__BackingField",
        "GuidingStar.<Spec>k__BackingField",
        "HammerTime.<Spec>k__BackingField",
        "HeavenlyDrill.<Spec>k__BackingField",
        "HuddleUp.<Spec>k__BackingField",
        "Intercept.<Spec>k__BackingField",
        "Knockdown.<Spec>k__BackingField",
        "KnowThyPlace.<Spec>k__BackingField",
        "Largesse.<Spec>k__BackingField",
        "Lift.<Spec>k__BackingField",
        "MasterOfStrategy.<Spec>k__BackingField",
        "Mayhem.<Spec>k__BackingField",
        "MeteorShower.<Spec>k__BackingField",
        "Mimic.<Spec>k__BackingField",
        "MindBlast.<Spec>k__BackingField",
        "MonarchsGaze.<Spec>k__BackingField",
        "Monologue.<Spec>k__BackingField",
        "NeutronAegis.<Spec>k__BackingField",
        "Nostalgia.<Spec>k__BackingField",
        "Omnislice.<Spec>k__BackingField",
        "PaleBlueDot.<Spec>k__BackingField",
        "Parry.<Spec>k__BackingField",
        "Patter.<Spec>k__BackingField",
        "Plot.<Spec>k__BackingField",
        "Production.<Spec>k__BackingField",
        "Prophesize.<Spec>k__BackingField",
        "Prowess.<Spec>k__BackingField",
        "Rally.<Spec>k__BackingField",
        "Reflect.<Spec>k__BackingField",
        "Rend.<Spec>k__BackingField",
        "Resonance.<Spec>k__BackingField",
        "RoyalGamble.<Spec>k__BackingField",
        "Royalties.<Spec>k__BackingField",
        "SeekingEdge.<Spec>k__BackingField",
        "SevenStars.<Spec>k__BackingField",
        "Shockwave.<Spec>k__BackingField",
        "SolarStrike.<Spec>k__BackingField",
        "SpectrumShift.<Spec>k__BackingField",
        "SpoilsOfBattle.<Spec>k__BackingField",
        "Stardust.<Spec>k__BackingField",
        "Stratagem.<Spec>k__BackingField",
        "StrikeSilent.<Spec>k__BackingField",
        "SwordSage.<Spec>k__BackingField",
        "TagTeam.<Spec>k__BackingField",
        "Terraforming.<Spec>k__BackingField",
        "TheBall.<Spec>k__BackingField",
        "TheGambit.<Spec>k__BackingField",
        "TheSealedThrone.<Spec>k__BackingField",
        "TheSmith.<Spec>k__BackingField",
        "Tutor.<Spec>k__BackingField",
        "Tyranny.<Spec>k__BackingField",
        "UltimateDefend.<Spec>k__BackingField",
        "UltimateStrike.<Spec>k__BackingField",
        "VoidForm.<Spec>k__BackingField",
        "Volley.<Spec>k__BackingField",
        "WroughtInWar.<Spec>k__BackingField",
        "BeatDown._count", "Bombardment._damage",
        "Bolas._damage", "BundleOfJoy._count", "ByrdSwoop._damage", "Catastrophe._count",
        "CollisionCourse._damage", "Convergence._nextTurnStars", "CosmicIndifference._block",
        "CrashLanding._damage", "DecisionsDecisions._draw", "DecisionsDecisions._repeat",
        "DefendRegent._block", "Equilibrium._block", "FallingStar._damage",
        "Fasten._extraBlock", "Genesis._starsPerTurn", "Glimmer._draw",
        "Glitterstream._block", "Glitterstream._nextTurnBlock", "Glow._stars",
        "GoldAxe._damagePerCardPlayed", "HandOfGreed._damage", "HandOfGreed._gold",
        "Hegemony._damage", "Hegemony._nextTurnEnergy", "HeirloomHammer._damage",
        "HiddenCache._stars", "HiddenCache._nextTurnStars", "HiddenGem._replay",
        "IAmInvincible._block", "Impatience._draw", "Jackpot._damage",
        "KinglyKick._damage", "KinglyPunch._damage", "KinglyPunch._increasePerDraw",
        "KnockoutBlow._damage", "LunarBlast._damage", "MakeItSo._damage",
        "MinionDiveBomb._damage", "MinionSacrifice._block", "MinionStrike._damage",
        "Neutralize._damage", "Neutralize._weak", "NeowsFury._damage",
        "NeowsFury._retrieveCount", "Panache._damage",
        "PanicButton._block", "ParticleWall._block", "Peck._hitCount",
        "PhotonCut._damage", "PhotonCut._draw", "PrepTime._vigor", "Purity._count",
        "Radiate._damage", "RefineBlade._forge", "Restlessness._draw",
        "Restlessness._energy", "RollingBoulder._startingDamage", "Salvo._damage",
        "SeekerStrike._damage", "ShiningStrike._damage", "SovereignBlade._damage",
        "StrikeRegent._damage", "SummonForth._forge", "Supermassive._damagePerGeneratedCard",
        "Adrenaline._energy", "Backflip._block", "Backstab._damage", "DaggerSpray._damage",
        "Dash._damage", "Dash._block", "Deflect._block", "DodgeAndRoll._block",
        "EchoingSlash._damage", "EscapePlan._block", "Expertise._cards", "Finisher._damage",
        "Flechettes._damage", "FlickFlack._damage", "GrandFinale._damage", "Pinpoint._damage",
        "Pounce._damage", "PreciseCut._baseDamage", "Predator._damage", "Ricochet._repeats",
        "Skewer._damage", "Slice._damage", "Untouchable._block",
        "Survivor._block",
        "Accuracy._amount", "Acrobatics._cards", "BladeOfInk._cards", "BladeDance._cards",
        "CloakAndDagger._cards", "DaggerThrow._damage", "FanOfKnives._cards",
        "LeadingStrike._damage", "MementoMori._baseDamage", "MementoMori._extraDamage",
        "PhantomBlades._amount", "Prepared._cards", "Reflex._cards", "Tactician._energy",
        "UpMySleeve._cards",
        "TheBomb._damage", "ThinkingAhead._draw", "ThrummingHatchet._damage",
        "ToricToughness._block", "Venerate._starsGranted", "MadScience._type",
        "PhantasmalGardener.<CurrentScale>k__BackingField",
        "Anger._damage", "AshenStrike._extraDamage", "Bash._damage", "Bash._vulnerable",
        "BattleTrance._cards", "Blaze._strength", "Bloodletting._energy", "BloodWall._block",
        "Bludgeon._damage", "Brand._strength", "Break._damage", "Break._vulnerable",
        "Breakthrough._damage", "Bully._damagePerVulnerable", "BurningPact._cards", "Cinder._damage",
        "Colossus._block", "Conflagration._repeats", "CrimsonMantle._block", "Cruelty._bonusPercent",
        "DefendIronclad._block", "DemonForm._strengthPerTurn", "Dismantle._damage", "Dominate._vulnerable",
        "DrumOfBattle._energy", "EvilEye._block", "ExpectAFight._baseBlock", "ExpectAFight._blockPerStrength",
        "Feed._damage", "Feed._maxHp", "FeelNoPain._blockPerExhaust", "FiendFire._damage",
        "FightMe._damage", "FightMe._strength", "FlameBarrier._block", "FlameBarrier._damageBack",
        "ForgottenRitual._energy", "GiantRock._damage", "Headbutt._damage", "Hemokinesis._damage",
        "HowlFromBeyond._damage", "Impervious._block", "Inferno._damage", "Inflame._strength",
        "IronWave._damage", "IronWave._block", "Juggernaut._damage", "Mangle._damage",
        "Mangle._strengthLoss", "Midnight._damage", "MoltenFist._damage", "NotYet._heal",
        "Offering._cards", "OneTwoPunch._attacks", "Outrage._damage", "PactsEnd._damage",
        "PerfectedStrike._extraDamagePerStrike", "Pillage._damage", "PommelStrike._damage", "PommelStrike._cards",
        "Pyre._energy", "Rage._power", "Rampage._increase", "Rupture._strength",
        "SecondWind._block", "SetupStrike._damage", "SetupStrike._strength", "ShrugItOff._block",
        "ShrugItOff._cards", "Spite._damage", "Spite._repeat", "Stomp._damage",
        "StoneArmor._plating", "StrikeIronclad._damage", "SwordBoomerang._damage", "SwordBoomerang._repeat",
        "Taunt._block", "Taunt._vulnerable", "TearAsunder._damage", "Thunderclap._damage",
        "Thunderclap._vulnerable", "Tremble._vulnerable", "TrueGrit._block", "TwinStrike._damage",
        "Unrelenting._damage", "Uppercut._damage", "Uppercut._power", "Vicious._cards",
        "Whirlwind._damage",
        // Defect card literals are fixed by model ID and upgrade level. Claw and GeneticAlgorithm
        // can evolve during combat and therefore contribute their private state above.
        "AdaptiveStrike._damage", "AllForOne._damage", "BallLightning._damage", "Barrage._damage",
        "BeamCell._damage", "BeamCell._vulnerable", "BiasedCognition._focus", "BiasedCognition._decay",
        "BoostAway._block", "BootSequence._block", "Buffer._buffer", "BulkUp._orbSlots",
        "BulkUp._strength", "BulkUp._dexterity", "Capacitor._slots", "Chaos._repeat",
        "ChargeBattery._block", "ChargeBattery._energy", "ColdSnap._damage", "Compact._block",
        "CompileDriver._damage", "ConsumingShadow._repeat", "ConsumingShadow._power", "Coolant._power",
        "Coolheaded._cards", "CreativeAi._power", "DefendDefect._block", "Defragment._focus",
        "EchoForm._power", "EnergySurge._energy", "Feral._power", "FightThrough._block",
        "FlakCannon._damage", "FocusedStrike._damage", "FocusedStrike._focus", "Ftl._damage",
        "Ftl._playMax", "Fuel._energy", "Glacier._block", "Glasswork._block",
        "GoForTheEyes._damage", "GoForTheEyes._weak", "GunkUp._damage", "Hailstorm._amount",
        "HelixDrill._damage", "Hibernate._frostCount", "Hologram._block", "Hotfix._focus",
        "Hyperbeam._damage", "Hyperbeam._focusLoss", "IceLance._damage", "ImitationLearning._amount",
        "Iteration._amount", "Leap._block", "LightningRod._block", "LightningRod._rodAmount",
        "Loop._amount", "MeteorStrike._damage", "Modded._cards", "MomentumStrike._damage",
        "Null._damage", "Null._weak", "OneForAll._amount", "Overclock._cards",
        "Reboot._cards", "Refract._damage", "RocketPunch._damage", "RocketPunch._cards",
        "Scavenge._energy", "Scrape._damage", "Scrape._cards", "ShadowShield._block",
        "Shatter._damage", "Skim._cards", "Smokestack._power", "Storm._power",
        "StrikeDefect._damage", "Sunder._damage", "Supercritical._energy", "SweepingBeam._damage",
        "Synchronize._focusPerDistinctOrb", "Synthesis._damage", "TeslaCoil._damage", "Thunder._power",
        "Turbo._energy", "Uproar._damage",
    ];

    public static bool TryGetClassification(
        Type type,
        string fieldName,
        out CombatPrivateStateClassification classification)
    {
        string key = $"{type.Name}.{fieldName}";
        if (ContributorFields.Contains(key))
        {
            classification = CombatPrivateStateClassification.Contributor;
            return true;
        }

        if (TransientFields.Contains(key))
        {
            classification = CombatPrivateStateClassification.TransientBoundary;
            return true;
        }

        if (DerivedFields.Contains(key))
        {
            classification = CombatPrivateStateClassification.DerivedPublicState;
            return true;
        }

        if (IgnoredFields.ContainsKey(key))
        {
            classification = CombatPrivateStateClassification.IgnoredNoFutureBehavior;
            return true;
        }

        classification = default;
        return false;
    }

    public static bool TryGetIgnoredReason(Type type, string fieldName, out string reason) =>
        IgnoredFields.TryGetValue($"{type.Name}.{fieldName}", out reason!);
}
