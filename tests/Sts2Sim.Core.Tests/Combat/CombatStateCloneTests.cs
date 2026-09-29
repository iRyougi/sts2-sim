namespace Sts2Sim.Core.Tests.Combat;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Ascension;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Rngs;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

[Collection("ModelDb")]
public sealed class CombatStateCloneTests
{
    public CombatStateCloneTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(WeakPower),
            typeof(VulnerablePower),
            typeof(StrengthPower),
            typeof(DivineRight),
            typeof(TrainingDummy),
            typeof(Sown),
            typeof(Glam),
            typeof(RingingPower),
            typeof(FirePotion),
            typeof(CloneRuntimeMonster),
            typeof(CloneTransientMonster),
            typeof(IllusionPower),
            typeof(MinionPower),
            typeof(Abundance),
            typeof(BlackHole),
            typeof(BlackHolePower),
            typeof(ChildOfTheStars),
            typeof(ChildOfTheStarsPower),
            typeof(Genesis),
            typeof(GenesisPower),
            typeof(UnsettlingLamp),
            typeof(DampenPower),
            typeof(Afterimage),
            typeof(AfterimagePower),
            typeof(Accuracy),
            typeof(AccuracyPower),
        });
    }

    [Theory]
    [InlineData(typeof(Afterimage))]
    [InlineData(typeof(Accuracy))]
    public async Task Clone_DampenRestoresDetachedPowerCardWithoutMutatingOriginal(Type cardType)
    {
        (CombatState original, CombatEngine engine) = await NewStartedCombat($"clone-dampen-{cardType.Name}");
        Player player = original.Players[0];
        CardModel origin = (CardModel)ModelDb.Get(cardType).MutableClone();
        origin.AssignOwner(player);
        // A retained detached card may itself have a detached CloneOf origin.
        origin.ExhaustOnNextPlay = true;
        CardModel card = origin.CreateClone();
        Assert.True(origin.ExhaustOnNextPlay);
        Assert.False(card.ExhaustOnNextPlay);
        CardCmd.Upgrade(card);
        CardPileCmd.Add(card, PileType.Hand, CardPilePosition.Bottom);
        DampenPower dampen = (DampenPower)(await PowerCmd.Apply<DampenPower>(
            original, player.Creature, 1m, original.Enemies[0], null))!;
        dampen.AddCaster(original.Enemies[0]);
        await engine.PlayCardAsync(player, card, null);
        Assert.Null(card.Pile);
        Assert.Equal(0, card.CurrentUpgradeLevel);

        CombatState clone = original.Clone(out CombatCloneMap map);
        DampenPower clonedDampen = Assert.IsType<DampenPower>(clone.Players[0].Creature.GetPower<DampenPower>());
        CardModel clonedCard = Assert.Single(clonedDampen.EnumerateCombatCloneCards());
        Assert.True(ReferenceEquals(clonedCard, map.Card(card)), $"cardType={cardType.Name}: detached card mapping");
        Assert.NotSame(card, clonedCard);
        Assert.Same(clone.Players[0], clonedCard.Owner);
        Assert.Null(clonedCard.Pile);
        Assert.NotSame(origin, clonedCard.CloneOf);
        Assert.Same(clone.Players[0], clonedCard.CloneOf!.Owner);

        await PowerCmd.Remove(clonedDampen);
        Assert.Equal(1, clonedCard.CurrentUpgradeLevel);
        Assert.Equal(0, card.CurrentUpgradeLevel);
        await PowerCmd.Remove(dampen);
        Assert.Equal(1, card.CurrentUpgradeLevel);
    }

    [Fact]
    public async Task Clone_MutationsDoNotCrossContaminateEitherCombatGraph()
    {
        (CombatState original, _) = await NewStartedCombat("clone-independent-graph");
        CombatState clone = original.Clone();
        Player originalPlayer = original.Players[0];
        Player clonedPlayer = clone.Players[0];
        Creature originalEnemy = original.Enemies[0];
        Creature clonedEnemy = clone.Enemies[0];
        CardModel originalCard = originalPlayer.PlayerCombatState!.Hand.Cards[0];
        CardModel clonedCard = clonedPlayer.PlayerCombatState!.Hand.Cards[0];
        int originalRunRngCounter = original.RunState.Rng.CombatCardGeneration.Counter;
        int originalMonsterRngCounter = originalEnemy.Monster!.Rng.Counter;
        int originalPlayerRngCounter = originalPlayer.PlayerRng.Rewards.Counter;

        Assert.NotSame(original.RunState, clone.RunState);
        Assert.NotSame(originalPlayer, clonedPlayer);
        Assert.NotSame(originalPlayer.Creature, clonedPlayer.Creature);
        Assert.NotSame(originalPlayer.PlayerCombatState, clonedPlayer.PlayerCombatState);
        Assert.NotSame(originalCard, clonedCard);
        Assert.NotSame(originalEnemy, clonedEnemy);
        Assert.NotSame(originalEnemy.Monster, clonedEnemy.Monster);

        clone.RunState.Rng.CombatCardGeneration.NextInt(100);
        clonedEnemy.Monster!.Rng.NextInt(100);
        clonedPlayer.PlayerRng.Rewards.NextInt(100);
        clonedPlayer.Creature.LoseHpInternal(7m, ValueProp.Unblockable);
        clonedEnemy.LoseHpInternal(9m, ValueProp.Unblockable);
        clonedPlayer.PlayerCombatState.DiscardPile.AddInternal(clonedCard);
        var clonedStrength = (StrengthPower)ModelDb.Power<StrengthPower>().MutableClone();
        clonedStrength.ApplyInternal(clonedEnemy, 3m);

        Assert.Equal(originalRunRngCounter, original.RunState.Rng.CombatCardGeneration.Counter);
        Assert.Equal(originalMonsterRngCounter, originalEnemy.Monster.Rng.Counter);
        Assert.Equal(originalPlayerRngCounter, originalPlayer.PlayerRng.Rewards.Counter);
        Assert.Equal(originalPlayer.Character.StartingHp, originalPlayer.Creature.CurrentHp);
        Assert.Equal(originalEnemy.MaxHp, originalEnemy.CurrentHp);
        Assert.Same(originalPlayer.PlayerCombatState.Hand, originalCard.Pile);
        Assert.Empty(originalEnemy.Powers);

        originalPlayer.Creature.LoseHpInternal(5m, ValueProp.Unblockable);
        originalEnemy.LoseHpInternal(4m, ValueProp.Unblockable);
        originalPlayer.PlayerCombatState.DiscardPile.AddInternal(originalCard);

        Assert.Equal(clonedPlayer.Character.StartingHp - 7, clonedPlayer.Creature.CurrentHp);
        Assert.Equal(clonedEnemy.MaxHp - 9, clonedEnemy.CurrentHp);
        Assert.Same(clonedPlayer.PlayerCombatState.DiscardPile, clonedCard.Pile);
        Assert.Single(clonedEnemy.Powers);
    }

    [Theory]
    [InlineData(typeof(Sown))]
    [InlineData(typeof(Glam))]
    public async Task Clone_PreservesDisabledEnchantmentStatus(Type enchantmentType)
    {
        (CombatState original, _) = await NewStartedCombat("clone-disabled-enchantment");
        CardModel originalCard = original.Players[0].PlayerCombatState!.Hand.Cards[0];
        var originalEnchantment = (EnchantmentModel)ModelDb
            .Get(enchantmentType)
            .MutableClone();
        originalEnchantment.AssignMagnitude(1m);
        originalCard.AttachEnchantment(originalEnchantment);

        switch (originalEnchantment)
        {
            case Sown:
                await originalEnchantment.OnPlay(originalCard);
                break;
            case Glam:
                originalEnchantment.EnchantPlayCount(1);
                break;
            default:
                throw new InvalidOperationException(
                    $"Unsupported test enchantment {enchantmentType.Name}.");
        }
        Assert.Equal(EnchantmentStatus.Disabled, originalEnchantment.Status);

        CombatState clone = original.Clone();

        CardModel clonedCard = clone.Players[0].PlayerCombatState!.Hand.Cards[0];
        EnchantmentModel clonedEnchantment = Assert.Single(clonedCard.Enchantments);
        Assert.NotSame(originalEnchantment, clonedEnchantment);
        Assert.Equal(enchantmentType, clonedEnchantment.GetType());
        Assert.Equal(EnchantmentStatus.Disabled, clonedEnchantment.Status);
    }

    [Fact]
    public async Task Clone_NextRngValuesMatchOriginalAndIndependentControl()
    {
        (CombatState original, _) = await NewStartedCombat("clone-rng-next-values");
        (CombatState control, _) = await NewStartedCombat("clone-rng-next-values");
        CombatState clone = original.Clone();

        foreach (RunRngType type in Enum.GetValues<RunRngType>())
        {
            AssertNextRngValuesMatchAndAreIndependent(
                control.RunState.Rng.GetRng(type),
                original.RunState.Rng.GetRng(type),
                clone.RunState.Rng.GetRng(type));
        }

        foreach (PlayerRngType type in Enum.GetValues<PlayerRngType>())
        {
            AssertNextRngValuesMatchAndAreIndependent(
                control.Players[0].PlayerRng.GetRng(type),
                original.Players[0].PlayerRng.GetRng(type),
                clone.Players[0].PlayerRng.GetRng(type));
        }

        AssertNextRngValuesMatchAndAreIndependent(
            control.Enemies[0].Monster!.Rng,
            original.Enemies[0].Monster!.Rng,
            clone.Enemies[0].Monster!.Rng);
    }

    [Fact]
    public void Clone_KeyedSemanticsAndSequentialRngHotPathsArePreserved()
    {
        var runState = new CloneTestRunState("clone-keyed-rng", useSemanticKeys: true);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var original = new CombatState(runState);
        original.AddMonster(
            (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);

        CombatState clone = original.Clone();
        const string runKey = "floor=3/combat=2/turn=4/source_card_uid=17";
        const string playerKey = "floor=3/room_kind=monster/slot=card_0";

        int originalRunValue = original.RunState.Rng
            .ForSemanticKey(RunRngType.CombatCardGeneration, runKey)
            .NextInt(1_000_000);
        int clonedRunValue = clone.RunState.Rng
            .ForSemanticKey(RunRngType.CombatCardGeneration, runKey)
            .NextInt(1_000_000);
        int originalPlayerValue = original.Players[0].PlayerRng
            .ForSemanticKey(PlayerRngType.Rewards, playerKey)
            .NextInt(1_000_000);
        int clonedPlayerValue = clone.Players[0].PlayerRng
            .ForSemanticKey(PlayerRngType.Rewards, playerKey)
            .NextInt(1_000_000);

        Assert.Equal(originalRunValue, clonedRunValue);
        Assert.Equal(originalPlayerValue, clonedPlayerValue);
        Assert.Throws<InvalidOperationException>(
            () => clone.RunState.Rng.CombatCardGeneration.NextInt(100));
        Assert.Throws<InvalidOperationException>(
            () => clone.Players[0].PlayerRng.Rewards.NextInt(100));

        var sequentialRun = new CloneTestRunState("sequential-rng-hot-path");
        Player sequentialPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), sequentialRun);
        sequentialRun.AddPlayer(sequentialPlayer);
        var sequentialCombat = new CombatState(sequentialRun);
        Creature sequentialEnemy = sequentialCombat.AddMonster(
            (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        CardModel sequentialCard = sequentialPlayer.Deck.Cards[0];
        var sequentialPotion = (FirePotion)ModelDb.Potion<FirePotion>().MutableClone();
        sequentialPlayer.AddPotionInternal(sequentialPotion);

        // Warm the JIT before measuring. Sequential fidelity mode must use the original streams
        // and a singleton no-op scope without constructing semantic keys or allocating wrappers.
        Assert.Same(sequentialRun.Rng.Shuffle, sequentialCombat.NextShuffleRng());
        Assert.Same(sequentialRun.Rng.MonsterAi, sequentialCombat.MonsterAiRng(sequentialEnemy));
        sequentialCombat.BeginCardRngScope(sequentialCard).Dispose();
        sequentialCombat.BeginPotionRngScope(sequentialPotion).Dispose();
        sequentialCombat.BeginMonsterRngScope(sequentialEnemy).Dispose();
        sequentialCombat.BeginPhaseRngScope("warmup").Dispose();

        long combatBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            Assert.Same(sequentialRun.Rng.Shuffle, sequentialCombat.NextShuffleRng());
            Assert.Same(sequentialRun.Rng.MonsterAi, sequentialCombat.MonsterAiRng(sequentialEnemy));
            sequentialCombat.BeginCardRngScope(sequentialCard).Dispose();
            sequentialCombat.BeginPotionRngScope(sequentialPotion).Dispose();
            sequentialCombat.BeginMonsterRngScope(sequentialEnemy).Dispose();
            sequentialCombat.BeginPhaseRngScope("measured").Dispose();
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - combatBefore);

        var sequentialRng = new Rng(0xC0FFEEuL);
        DrawSequentialRngOperations(sequentialRng);
        long rngBefore = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            DrawSequentialRngOperations(sequentialRng);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - rngBefore);
    }

    [Fact]
    public async Task Clone_PreservesEveryMutableGraphFieldWithIndependentReferences()
    {
        (CombatState original, _) =
            await NewStartedCombat("clone-full-graph", typeof(CloneRuntimeMonster));
        Player player = original.Players[0];
        PlayerCombatState playerCombat = player.PlayerCombatState!;
        Creature enemy = original.Enemies[0];
        player.AddPotionInternal((FirePotion)ModelDb.Potion<FirePotion>().MutableClone());
        playerCombat.Hand.Cards[0].AddEnergyCostThisCombat(2);
        playerCombat.Hand.Cards[1].MakeTemporaryFreeThisTurn();
        playerCombat.Hand.Cards[2].MakeFreeUntilPlayed();
        playerCombat.Hand.Cards[3].SetTemporaryCostOverrideThisTurn(4);
        playerCombat.Hand.Cards[4].SetTemporaryCostOverrideThisCombat(3);

        var strength = (StrengthPower)ModelDb.Power<StrengthPower>().MutableClone();
        strength.Applier = player.Creature;
        strength.SkipNextDurationTick = true;
        strength.ApplyInternal(enemy, 3m);
        var ringing = (RingingPower)ModelDb.Power<RingingPower>().MutableClone();
        ringing.ApplyInternal(player.Creature, 1m);
        await ringing.AfterApplied(applier: null, cardSource: null);
        await enemy.Monster!.PerformMove();

        Creature escaped = original.AddMonster(
            (MonsterModel)ModelDb.Monster<CloneRuntimeMonster>().MutableClone(), CombatSide.Enemy);
        original.CreatureEscaped(escaped);
        Creature removed = original.AddMonster(
            (MonsterModel)ModelDb.Monster<CloneRuntimeMonster>().MutableClone(), CombatSide.Enemy);
        original.RemoveCreature(removed);

        CombatState clone = original.Clone(out CombatCloneMap map);

        for (int playerIndex = 0; playerIndex < original.Players.Count; playerIndex++)
        {
            Player sourcePlayer = original.Players[playerIndex];
            Player targetPlayer = clone.Players[playerIndex];
            Assert.True(ReferenceEquals(targetPlayer, map.Player(sourcePlayer)),
                $"seed=clone-full-graph, player={playerIndex}: player mapping");

            IEnumerable<(CardPile Source, CardPile Target)> piles =
                new[] { (sourcePlayer.Deck, targetPlayer.Deck) }.Concat(
                    sourcePlayer.PlayerCombatState!.AllPiles.Zip(targetPlayer.PlayerCombatState!.AllPiles));
            foreach ((CardPile sourcePile, CardPile targetPile) in piles)
            {
                for (int cardIndex = 0; cardIndex < sourcePile.Cards.Count; cardIndex++)
                {
                    Assert.True(ReferenceEquals(targetPile.Cards[cardIndex], map.Card(sourcePile.Cards[cardIndex])),
                        $"seed=clone-full-graph, player={playerIndex}, pile={sourcePile.Type}, card={cardIndex}: card mapping");
                }
            }
        }

        Creature[] sourceCreatures = original.Allies.Concat(original.Enemies)
            .Concat(original.EscapedCreatures).Concat(original.RemovedCreatures).ToArray();
        Creature[] targetCreatures = clone.Allies.Concat(clone.Enemies)
            .Concat(clone.EscapedCreatures).Concat(clone.RemovedCreatures).ToArray();
        Assert.Equal(sourceCreatures.Length, targetCreatures.Length);
        for (int creatureIndex = 0; creatureIndex < sourceCreatures.Length; creatureIndex++)
        {
            Assert.True(ReferenceEquals(targetCreatures[creatureIndex], map.Creature(sourceCreatures[creatureIndex])),
                $"seed=clone-full-graph, creature={creatureIndex}: creature mapping");
        }

        AssertEquivalentMutableCombatFields(original, clone);

        MonsterMoveStateMachine originalMachine = enemy.Monster.MoveStateMachine!;
        MonsterModel clonedMonster = clone.Enemies[0].Monster!;
        MonsterMoveStateMachine clonedMachine = clonedMonster.MoveStateMachine!;
        MoveState originalNext = originalMachine.RollMove(
            original.Allies,
            enemy,
            enemy.Monster.Rng);
        MoveState clonedNext = clonedMachine.RollMove(
            clone.Allies,
            clone.Enemies[0],
            clonedMonster.Rng);
        Assert.Equal(originalNext.StateId, clonedNext.StateId);
        Assert.Equal(
            originalMachine.StateLog.Select(state => state.Id),
            clonedMachine.StateLog.Select(state => state.Id));

        var unstartedRun = new CloneTestRunState("clone-full-graph-unstarted");
        Player unstartedPlayer = Player.CreateForNewRun(ModelDb.Character<Regent>(), unstartedRun);
        unstartedRun.AddPlayer(unstartedPlayer);
        var unstarted = new CombatState(unstartedRun);
        CombatState unstartedClone = unstarted.Clone(out CombatCloneMap unstartedMap);
        Assert.True(ReferenceEquals(unstartedClone.Players[0].Creature,
                unstartedMap.Creature(unstartedPlayer.Creature)),
            "seed=clone-full-graph-unstarted: player creature mapping");
    }

    [Fact]
    public async Task Clone_ReviveMoveRebindsCallbackAndRuntimeToClonedIllusionPower()
    {
        (CombatState original, _) =
            await NewStartedCombat("clone-transient-revive", typeof(CloneTransientMonster));
        Creature originalEnemy = original.Enemies[0];
        IllusionPower originalPower = Assert.IsType<IllusionPower>(
            await PowerCmd.Apply<IllusionPower>(
                original,
                originalEnemy,
                1m,
                originalEnemy,
                cardSource: null));

        await CreatureCmd.Damage(
            original,
            new[] { originalEnemy },
            originalEnemy.CurrentHp,
            ValueProp.Unblockable,
            dealer: null,
            cardSource: null,
            cardPlay: null);

        MoveState originalMove = Assert.IsType<MoveState>(originalEnemy.Monster!.NextMove);
        Assert.Equal("REVIVE_MOVE", originalMove.StateId);
        Assert.IsType<HealIntent>(Assert.Single(originalMove.Intents));
        Assert.DoesNotContain(originalMove.StateId, originalEnemy.Monster.MoveStateMachine!.States.Keys);
        Assert.True(originalPower.IsReviving);
        Assert.True(originalEnemy.IsDead);

        CombatState clone = original.Clone();

        Creature clonedEnemy = clone.Enemies[0];
        MonsterModel clonedMonster = clonedEnemy.Monster!;
        IllusionPower clonedPower = Assert.IsType<IllusionPower>(clonedEnemy.GetPower<IllusionPower>());
        MoveState clonedMove = Assert.IsType<MoveState>(clonedMonster.NextMove);
        Assert.NotSame(originalMove, clonedMove);
        Assert.NotSame(Assert.Single(originalMove.Intents), Assert.Single(clonedMove.Intents));
        Assert.IsType<HealIntent>(Assert.Single(clonedMove.Intents));
        Assert.DoesNotContain(clonedMove.StateId, clonedMonster.MoveStateMachine!.States.Keys);
        Assert.NotSame(originalPower, clonedPower);
        Assert.True(clonedPower.IsReviving);
        Assert.True(clonedEnemy.IsDead);

        await clonedMonster.PerformMove();

        Assert.False(clonedPower.IsReviving);
        Assert.True(clonedEnemy.IsAlive);
        Assert.Equal(clonedEnemy.MaxHp, clonedEnemy.CurrentHp);
        Assert.True(clonedMove.CanTransitionAway);
        Assert.True(originalPower.IsReviving);
        Assert.True(originalEnemy.IsDead);
        Assert.False(originalMove.CanTransitionAway);
    }

    [Fact]
    public async Task Clone_StunnedMoveOwnsIndependentMoveIntentAndRuntimeState()
    {
        (CombatState original, _) =
            await NewStartedCombat("clone-transient-stunned", typeof(CloneTransientMonster));
        Creature originalEnemy = original.Enemies[0];
        await CreatureCmd.Stun(originalEnemy, "SECOND");
        MoveState originalMove = Assert.IsType<MoveState>(originalEnemy.Monster!.NextMove);
        Assert.Equal("STUNNED", originalMove.StateId);
        Assert.IsType<StunIntent>(Assert.Single(originalMove.Intents));
        Assert.DoesNotContain(originalMove.StateId, originalEnemy.Monster.MoveStateMachine!.States.Keys);

        CombatState clone = original.Clone();

        Creature clonedEnemy = clone.Enemies[0];
        MonsterModel clonedMonster = clonedEnemy.Monster!;
        MoveState clonedMove = Assert.IsType<MoveState>(clonedMonster.NextMove);
        Assert.NotSame(originalMove, clonedMove);
        Assert.NotSame(Assert.Single(originalMove.Intents), Assert.Single(clonedMove.Intents));
        Assert.IsType<StunIntent>(Assert.Single(clonedMove.Intents));
        Assert.DoesNotContain(clonedMove.StateId, clonedMonster.MoveStateMachine!.States.Keys);
        Assert.False(originalMove.CanTransitionAway);
        Assert.False(clonedMove.CanTransitionAway);

        await clonedMonster.PerformMove();
        Assert.True(clonedMove.CanTransitionAway);
        clonedMonster.RollMove(clone.Allies);

        Assert.Equal("SECOND", clonedMonster.NextMove!.StateId);
        Assert.False(originalMove.CanTransitionAway);
        Assert.Same(originalMove, originalEnemy.Monster.NextMove);
    }

    [Fact]
    public async Task Clone_PlayedAbundancePreservesIndependentCandidateGraph()
    {
        (CombatState original, _) = await NewStartedCombat("clone-abundance-candidates");
        Player originalPlayer = original.Players[0];
        PlayerCombatState originalCombat = originalPlayer.PlayerCombatState!;
        var originalAbundance = (Abundance)ModelDb.Card<Abundance>().MutableClone();
        originalAbundance.AssignOwner(originalPlayer);
        originalCombat.Hand.AddInternal(originalAbundance);

        await originalAbundance.PlayAsync(target: null);

        Assert.Equal(3, originalAbundance.GeneratedCandidates.Count);
        Assert.Same(
            originalAbundance.GeneratedCandidates[0],
            Assert.Single(
                originalCombat.Hand.Cards,
                card => ReferenceEquals(card, originalAbundance.GeneratedCandidates[0])));

        CardModel originalSecond = originalAbundance.GeneratedCandidates[1];
        CardModel originalThird = originalAbundance.GeneratedCandidates[2];
        foreach (CardModel candidate in new[] { originalSecond, originalThird })
        {
            Assert.Same(originalPlayer, candidate.Owner);
            Assert.Same(original, candidate.CombatState);
        }
        originalSecond.MakeTemporaryFreeThisTurn();
        originalSecond.SetTemporaryCostOverrideThisCombat(3);
        originalSecond.AddEnergyCostThisCombat(2);
        var originalEnchantment = (Glam)ModelDb.Get(typeof(Glam)).MutableClone();
        originalEnchantment.AssignMagnitude(2m);
        originalSecond.AttachEnchantment(originalEnchantment);
        Assert.Equal(2, originalEnchantment.EnchantPlayCount(1));
        Assert.Equal(EnchantmentStatus.Disabled, originalEnchantment.Status);
        originalThird.MakeFreeUntilPlayed();
        originalThird.SetTemporaryCostOverrideThisTurn(4);

        CombatState clone = original.Clone();

        PlayerCombatState clonedCombat = clone.Players[0].PlayerCombatState!;
        Abundance clonedAbundance = Assert.Single(clonedCombat.ExhaustPile.Cards.OfType<Abundance>());
        Assert.Equal(3, clonedAbundance.GeneratedCandidates.Count);
        for (int index = 0; index < originalAbundance.GeneratedCandidates.Count; index++)
        {
            CardModel originalCandidate = originalAbundance.GeneratedCandidates[index];
            CardModel clonedCandidate = clonedAbundance.GeneratedCandidates[index];
            Assert.NotSame(originalCandidate, clonedCandidate);
            Assert.Equal(originalCandidate.GetType(), clonedCandidate.GetType());
            Assert.Equal(originalCandidate.CurrentUpgradeLevel, clonedCandidate.CurrentUpgradeLevel);
            Assert.Equal(originalCandidate.EnergyCost, clonedCandidate.EnergyCost);
            if (index > 0)
            {
                Assert.Same(originalPlayer, originalCandidate.Owner);
                Assert.Same(original, originalCandidate.CombatState);
                Assert.Same(clone.Players[0], clonedCandidate.Owner);
                Assert.Same(clone, clonedCandidate.CombatState);
                Assert.Equal(originalCandidate.TemporaryFreeThisTurn, clonedCandidate.TemporaryFreeThisTurn);
                Assert.Equal(originalCandidate.TemporaryFreeUntilPlayed, clonedCandidate.TemporaryFreeUntilPlayed);
                Assert.Equal(originalCandidate.TemporaryCostOverrideThisTurn, clonedCandidate.TemporaryCostOverrideThisTurn);
                Assert.Equal(originalCandidate.TemporaryCostOverrideThisCombat, clonedCandidate.TemporaryCostOverrideThisCombat);
                Assert.Equal(originalCandidate.Enchantments.Count, clonedCandidate.Enchantments.Count);
                foreach ((EnchantmentModel source, EnchantmentModel target) in
                         originalCandidate.Enchantments.Zip(clonedCandidate.Enchantments))
                {
                    Assert.NotSame(source, target);
                    Assert.Same(clonedCandidate, target.Owner);
                    Assert.Equal(source.Status, target.Status);
                    Assert.Equal(source.Magnitude, target.Magnitude);
                }
            }
        }

        Assert.Same(
            clonedAbundance.GeneratedCandidates[0],
            Assert.Single(
                clonedCombat.Hand.Cards,
                card => ReferenceEquals(card, clonedAbundance.GeneratedCandidates[0])));
        int originalSecondCost = originalAbundance.GeneratedCandidates[1].EnergyCost;
        EnchantmentModel clonedSecondEnchantment = Assert.Single(
            clonedAbundance.GeneratedCandidates[1].Enchantments);

        clonedAbundance.GeneratedCandidates[1].AddEnergyCostThisCombat(2);
        clonedAbundance.GeneratedCandidates[1].SetTemporaryCostOverrideThisCombat(9);
        clonedAbundance.GeneratedCandidates[1].BaseReplayCount = 7;
        clonedSecondEnchantment.AssignMagnitude(8m);

        Assert.Equal(originalSecondCost, originalAbundance.GeneratedCandidates[1].EnergyCost);
        // Native local modifiers fold in application order: the later absolute 9 overrides +2.
        Assert.Equal(9, clonedAbundance.GeneratedCandidates[1].EnergyCost);
        Assert.Equal(3, originalSecond.TemporaryCostOverrideThisCombat);
        Assert.Equal(0, originalSecond.BaseReplayCount);
        Assert.Equal(2m, originalEnchantment.Magnitude);
    }

    [Fact]
    public async Task Clone_OffMapOwnedAbundanceCandidatePreservesCombatRuntimeAndCloneOwnership()
    {
        (CombatState original, _) = await NewStartedCombat("clone-off-map-abundance-candidate");
        Player originalPlayer = original.Players[0];
        PlayerCombatState originalCombat = originalPlayer.PlayerCombatState!;
        var originalAbundance = (Abundance)ModelDb.Card<Abundance>().MutableClone();
        originalAbundance.AssignOwner(originalPlayer);
        originalCombat.Hand.AddInternal(originalAbundance);

        await originalAbundance.PlayAsync(target: null);

        CardModel originalCandidate = originalAbundance.GeneratedCandidates[0];
        await originalCandidate.PlayAsync(target: null);
        originalCandidate.MakeFreeUntilPlayed();
        originalCandidate.SetTemporaryCostOverrideThisTurn(4);
        originalCandidate.SetTemporaryCostOverrideThisCombat(3);
        originalCandidate.AddEnergyCostThisCombat(2);
        var originalEnchantment = (Sown)ModelDb.Get(typeof(Sown)).MutableClone();
        originalEnchantment.AssignMagnitude(2m);
        originalCandidate.AttachEnchantment(originalEnchantment);
        await originalEnchantment.OnPlay(originalCandidate);
        CardPileCmd.Remove(originalCandidate);
        Assert.Same(originalPlayer, originalCandidate.Owner);
        Assert.Same(original, originalCandidate.CombatState);
        Assert.Null(originalCandidate.Pile);
        Assert.Contains(originalCandidate, originalAbundance.GeneratedCandidates);
        Assert.Equal(EnchantmentStatus.Disabled, originalEnchantment.Status);

        CombatState clone = original.Clone();

        Player clonedPlayer = clone.Players[0];
        Abundance clonedAbundance = Assert.Single(
            clonedPlayer.PlayerCombatState!.ExhaustPile.Cards.OfType<Abundance>());
        CardModel clonedCandidate = clonedAbundance.GeneratedCandidates[0];
        EnchantmentModel clonedEnchantment = Assert.Single(clonedCandidate.Enchantments);
        Assert.NotSame(originalCandidate, clonedCandidate);
        Assert.Same(clonedPlayer, clonedCandidate.Owner);
        Assert.Same(clone, clonedCandidate.CombatState);
        Assert.Null(clonedCandidate.Pile);
        Assert.Equal(originalCandidate.TemporaryFreeThisTurn, clonedCandidate.TemporaryFreeThisTurn);
        Assert.Equal(originalCandidate.TemporaryFreeUntilPlayed, clonedCandidate.TemporaryFreeUntilPlayed);
        Assert.Equal(originalCandidate.TemporaryCostOverrideThisTurn, clonedCandidate.TemporaryCostOverrideThisTurn);
        Assert.Equal(originalCandidate.TemporaryCostOverrideThisCombat, clonedCandidate.TemporaryCostOverrideThisCombat);
        Assert.Equal(originalCandidate.EnergyCost, clonedCandidate.EnergyCost);
        Assert.NotSame(originalEnchantment, clonedEnchantment);
        Assert.Same(clonedCandidate, clonedEnchantment.Owner);
        Assert.Equal(originalEnchantment.Status, clonedEnchantment.Status);
        Assert.Equal(originalEnchantment.Magnitude, clonedEnchantment.Magnitude);

        clonedCandidate.SetTemporaryCostOverrideThisCombat(9);
        clonedCandidate.BaseReplayCount = 7;
        clonedEnchantment.AssignMagnitude(8m);

        Assert.Equal(3, originalCandidate.TemporaryCostOverrideThisCombat);
        Assert.Equal(0, originalCandidate.BaseReplayCount);
        Assert.Equal(2m, originalEnchantment.Magnitude);
    }

    [Fact]
    public async Task Clone_UnsettlingLampRemapsTriggeringCardAndFinishesIndependently()
    {
        (CombatState original, _) = await NewStartedCombat("clone-unsettling-lamp");
        Player originalPlayer = original.Players[0];
        Creature originalEnemy = original.Enemies[0];
        var originalLamp = (UnsettlingLamp)ModelDb.Relic<UnsettlingLamp>().MutableClone();
        originalLamp.AssignOwner(originalPlayer);
        originalPlayer.AddRelicInternal(originalLamp);
        var originalTrigger = (BlackHole)ModelDb.Card<BlackHole>().MutableClone();
        originalTrigger.AssignOwner(originalPlayer);
        originalPlayer.PlayerCombatState!.Hand.AddInternal(originalTrigger);
        var weak = (WeakPower)ModelDb.Power<WeakPower>().MutableClone();

        await originalLamp.BeforePowerAmountChanged(
            weak,
            1m,
            originalEnemy,
            originalPlayer.Creature,
            originalTrigger);

        Assert.Equal(
            2m,
            originalLamp.ModifyPowerAmountGivenMultiplicative(
                weak,
                originalPlayer.Creature,
                1m,
                originalEnemy,
                originalTrigger));

        CombatState clone = original.Clone();

        Player clonedPlayer = clone.Players[0];
        Creature clonedEnemy = clone.Enemies[0];
        UnsettlingLamp clonedLamp = Assert.Single(clonedPlayer.Relics.OfType<UnsettlingLamp>());
        BlackHole clonedTrigger = Assert.Single(
            clonedPlayer.PlayerCombatState!.Hand.Cards.OfType<BlackHole>());
        Assert.NotSame(originalLamp, clonedLamp);
        Assert.NotSame(originalTrigger, clonedTrigger);
        Assert.Equal(
            1m,
            clonedLamp.ModifyPowerAmountGivenMultiplicative(
                weak,
                clonedPlayer.Creature,
                1m,
                clonedEnemy,
                originalTrigger));
        Assert.Equal(
            2m,
            clonedLamp.ModifyPowerAmountGivenMultiplicative(
                weak,
                clonedPlayer.Creature,
                1m,
                clonedEnemy,
                clonedTrigger));

        await clonedLamp.AfterCardPlayed(new CardPlay
        {
            Card = clonedTrigger,
            Player = clonedPlayer,
            Target = clonedEnemy,
            ResultPile = PileType.Discard,
            Resources = new ResourceInfo(0, 0, 0, 0),
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 1,
        });

        Assert.Equal(
            1m,
            clonedLamp.ModifyPowerAmountGivenMultiplicative(
                weak,
                clonedPlayer.Creature,
                1m,
                clonedEnemy,
                clonedTrigger));
        Assert.Equal(
            2m,
            originalLamp.ModifyPowerAmountGivenMultiplicative(
                weak,
                originalPlayer.Creature,
                1m,
                originalEnemy,
                originalTrigger));
    }

    [Fact]
    public async Task Clone_ActiveUnsettlingLampWithUnmappedTriggerFailsDescriptively()
    {
        (CombatState original, _) = await NewStartedCombat("clone-active-unmapped-unsettling-lamp");
        Player originalPlayer = original.Players[0];
        Creature originalEnemy = original.Enemies[0];
        var originalLamp = (UnsettlingLamp)ModelDb.Relic<UnsettlingLamp>().MutableClone();
        originalLamp.AssignOwner(originalPlayer);
        originalPlayer.AddRelicInternal(originalLamp);
        var originalTrigger = (BlackHole)ModelDb.Card<BlackHole>().MutableClone();
        originalTrigger.AssignOwner(originalPlayer);
        originalPlayer.PlayerCombatState!.Hand.AddInternal(originalTrigger);
        var weak = (WeakPower)ModelDb.Power<WeakPower>().MutableClone();

        await originalLamp.BeforePowerAmountChanged(
            weak,
            1m,
            originalEnemy,
            originalPlayer.Creature,
            originalTrigger);
        CardPileCmd.Remove(originalTrigger);
        Assert.DoesNotContain(
            originalTrigger,
            originalPlayer.PlayerCombatState.AllPiles.SelectMany(pile => pile.Cards));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => original.Clone());
        Assert.Contains("UnsettlingLamp", exception.Message);
        Assert.Contains("active triggering card", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Clone_FinishedUnsettlingLampDropsUnmappedInactiveTrigger()
    {
        (CombatState original, _) = await NewStartedCombat("clone-finished-unsettling-lamp");
        Player originalPlayer = original.Players[0];
        Creature originalEnemy = original.Enemies[0];
        var originalLamp = (UnsettlingLamp)ModelDb.Relic<UnsettlingLamp>().MutableClone();
        originalLamp.AssignOwner(originalPlayer);
        originalPlayer.AddRelicInternal(originalLamp);
        var originalTrigger = (BlackHole)ModelDb.Card<BlackHole>().MutableClone();
        originalTrigger.AssignOwner(originalPlayer);
        originalPlayer.PlayerCombatState!.Hand.AddInternal(originalTrigger);
        var weak = (WeakPower)ModelDb.Power<WeakPower>().MutableClone();

        await originalLamp.BeforePowerAmountChanged(
            weak,
            1m,
            originalEnemy,
            originalPlayer.Creature,
            originalTrigger);
        await originalLamp.AfterCardPlayed(new CardPlay
        {
            Card = originalTrigger,
            Player = originalPlayer,
            Target = originalEnemy,
            ResultPile = PileType.None,
            Resources = new ResourceInfo(0, 0, 0, 0),
            IsAutoPlay = false,
            PlayIndex = 0,
            PlayCount = 1,
        });
        CardPileCmd.Remove(originalTrigger);
        Assert.DoesNotContain(
            originalTrigger,
            originalPlayer.PlayerCombatState.AllPiles.SelectMany(pile => pile.Cards));

        CombatState clone = original.Clone();

        Player clonedPlayer = clone.Players[0];
        UnsettlingLamp clonedLamp = Assert.Single(clonedPlayer.Relics.OfType<UnsettlingLamp>());
        Assert.Equal(
            1m,
            clonedLamp.ModifyPowerAmountGivenMultiplicative(
                weak,
                clonedPlayer.Creature,
                1m,
                clone.Enemies[0],
                originalTrigger));
        Assert.Equal(
            1m,
            originalLamp.ModifyPowerAmountGivenMultiplicative(
                weak,
                originalPlayer.Creature,
                1m,
                originalEnemy,
                originalTrigger));
    }

    [Fact]
    public void Clone_PreservesNextCreatureIdWithoutSharingTheCounter()
    {
        const string seed = "clone-creature-id";
        var run = new RunState(seed, new ActDefinition[]
        {
            new Overgrowth(), new Hive(), new Glory(),
        });
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        run.AdvanceToNextAct();
        run.AdvanceToNextAct();
        Assert.True(run.AddVisitedMapCoord(run.Map.StartingMapPoint.coord));
        var original = new CombatState(run);
        original.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        CombatState clone = original.Clone();
        CombatState cloneOfClone = clone.Clone();

        Creature clonedAddition = clone.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        Creature nestedAddition = cloneOfClone.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);
        Creature originalAddition = original.AddMonster(
            (TrainingDummy)ModelDb.Monster<TrainingDummy>().MutableClone(),
            CombatSide.Enemy);

        Assert.Equal(1u, clonedAddition.CombatId);
        Assert.Equal(1u, originalAddition.CombatId);
        Assert.Equal(2, clone.Enemies.Count);
        Assert.Equal(2, original.Enemies.Count);
        var originalRng = originalAddition.Monster!.Rng.ToSerializable();
        var clonedRng = clonedAddition.Monster!.Rng.ToSerializable();
        var nestedRng = nestedAddition.Monster!.Rng.ToSerializable();
        Assert.True(originalRng.Equals(clonedRng) && originalRng.Equals(nestedRng),
            $"seed={seed}, location={run.MapLocation}: original={originalRng}; clone={clonedRng}; nested={nestedRng}");
    }

    [Fact]
    public async Task DiscardedCloneSimulation_DoesNotChangeTheNextRealActionOutcome()
    {
        (CombatState searchedOriginal, CombatEngine searchedEngine) =
            await NewStartedCombat("clone-search-equivalence");
        (CombatState control, CombatEngine controlEngine) =
            await NewStartedCombat("clone-search-equivalence");
        string beforeSearch = Snapshot(searchedOriginal);

        CombatState branch = searchedOriginal.Clone();
        for (int step = 0; step < 4 && branch.Engine!.IsInProgress; step++)
        {
            (CardModel? card, Creature? target) = FirstLegalCard(branch);
            if (card is null)
            {
                await branch.Engine.EndPlayerTurnAsync();
            }
            else
            {
                await branch.Engine.PlayCardAsync(branch.Players[0], card, target);
            }
        }

        Assert.Equal(beforeSearch, Snapshot(searchedOriginal));

        (CardModel? searchedCard, Creature? searchedTarget) = FirstLegalCard(searchedOriginal);
        (CardModel? controlCard, Creature? controlTarget) = FirstLegalCard(control);
        Assert.NotNull(searchedCard);
        Assert.NotNull(controlCard);
        Assert.Equal(searchedCard.Id, controlCard.Id);

        await searchedEngine.PlayCardAsync(searchedOriginal.Players[0], searchedCard, searchedTarget);
        await controlEngine.PlayCardAsync(control.Players[0], controlCard, controlTarget);

        Assert.Equal(Snapshot(control), Snapshot(searchedOriginal));
        AssertEquivalentMutableCombatFields(control, searchedOriginal);
    }

    private static void AssertNextRngValuesMatchAndAreIndependent(
        Rng control,
        Rng original,
        Rng clone)
    {
        Assert.NotSame(control, original);
        Assert.NotSame(original, clone);
        Assert.NotSame(control, clone);

        int expected = control.NextInt(1_000_000);
        Assert.Equal(expected, original.NextInt(1_000_000));
        Assert.Equal(expected, clone.NextInt(1_000_000));

        clone.NextInt(1_000_000);

        Assert.Equal(control.Counter, original.Counter);
        Assert.Equal(control.Counter + 1, clone.Counter);
    }

    private static void AssertEquivalentMutableCombatFields(
        CombatState expected,
        CombatState actual)
    {
        Assert.NotSame(expected, actual);
        Assert.NotSame(expected.RunState, actual.RunState);
        Assert.NotSame(expected.RunState.Rng, actual.RunState.Rng);
        Assert.Equal(expected.RunState.Rng.StringSeed, actual.RunState.Rng.StringSeed);
        Assert.Equal(expected.RunState.Rng.Seed, actual.RunState.Rng.Seed);
        Assert.Same(expected.RunState.CurrentRoom, actual.RunState.CurrentRoom);
        Assert.Equal(expected.CurrentSide, actual.CurrentSide);
        Assert.Equal(expected.RoundNumber, actual.RoundNumber);
        Assert.NotNull(expected.Engine);
        Assert.NotNull(actual.Engine);
        Assert.NotSame(expected.Engine, actual.Engine);
        Assert.Same(actual, actual.Engine.State);
        Assert.Equal(expected.Engine.IsInProgress, actual.Engine.IsInProgress);
        Assert.Equal(expected.Engine.Won, actual.Engine.Won);
        Assert.Null(actual.Engine.Observer);

        foreach (RunRngType type in Enum.GetValues<RunRngType>())
        {
            Rng expectedRng = expected.RunState.Rng.GetRng(type);
            Rng actualRng = actual.RunState.Rng.GetRng(type);
            Assert.NotSame(expectedRng, actualRng);
            Assert.Equal(expectedRng.Counter, actualRng.Counter);
        }

        Assert.Equal(expected.Players.Count, actual.Players.Count);
        var playerMap = new Dictionary<Player, Player>(ReferenceEqualityComparer.Instance);
        var cardMap = new Dictionary<CardModel, CardModel>(ReferenceEqualityComparer.Instance);
        for (int playerIndex = 0; playerIndex < expected.Players.Count; playerIndex++)
        {
            Player expectedPlayer = expected.Players[playerIndex];
            Player actualPlayer = actual.Players[playerIndex];
            playerMap.Add(expectedPlayer, actualPlayer);
            Assert.NotSame(expectedPlayer, actualPlayer);
            Assert.Same(expectedPlayer.Character, actualPlayer.Character);
            Assert.Same(actual.RunState, actualPlayer.RunState);
            Assert.Equal(expectedPlayer.IsActiveForHooks, actualPlayer.IsActiveForHooks);
            Assert.Equal(expectedPlayer.Gold, actualPlayer.Gold);
            Assert.Equal(expectedPlayer.CardRemovalsUsed, actualPlayer.CardRemovalsUsed);
            Assert.Equal(expectedPlayer.MaxEnergy, actualPlayer.MaxEnergy);
            Assert.NotSame(expectedPlayer.RelicGrabBag, actualPlayer.RelicGrabBag);
            Assert.NotSame(expectedPlayer.Odds, actualPlayer.Odds);
            Assert.NotSame(expectedPlayer.PlayerRng, actualPlayer.PlayerRng);
            foreach (PlayerRngType type in Enum.GetValues<PlayerRngType>())
            {
                Rng expectedRng = expectedPlayer.PlayerRng.GetRng(type);
                Rng actualRng = actualPlayer.PlayerRng.GetRng(type);
                Assert.NotSame(expectedRng, actualRng);
                Assert.Equal(expectedRng.Counter, actualRng.Counter);
            }

            AssertEquivalentPile(expectedPlayer.Deck, actualPlayer.Deck, actualPlayer, cardMap);
            Assert.NotNull(expectedPlayer.PlayerCombatState);
            Assert.NotNull(actualPlayer.PlayerCombatState);
            PlayerCombatState expectedCombat = expectedPlayer.PlayerCombatState;
            PlayerCombatState actualCombat = actualPlayer.PlayerCombatState;
            Assert.NotSame(expectedCombat, actualCombat);
            Assert.Equal(expectedCombat.Energy, actualCombat.Energy);
            Assert.Equal(expectedCombat.Stars, actualCombat.Stars);
            Assert.Equal(expectedCombat.Phase, actualCombat.Phase);
            Assert.Equal(expectedCombat.TurnNumber, actualCombat.TurnNumber);
            Assert.Equal(expectedCombat.StarsGainedThisTurn, actualCombat.StarsGainedThisTurn);
            Assert.Equal(expectedCombat.SkillCardsPlayedThisTurn, actualCombat.SkillCardsPlayedThisTurn);
            Assert.Equal(expectedCombat.CardsPlayedThisCombat, actualCombat.CardsPlayedThisCombat);
            Assert.Equal(expectedCombat.CardsGeneratedThisCombat, actualCombat.CardsGeneratedThisCombat);
            Assert.Equal(expectedCombat.CardsPlayedThisTurn, actualCombat.CardsPlayedThisTurn);
            Assert.Equal(expectedCombat.CardPlaysStartedThisTurn, actualCombat.CardPlaysStartedThisTurn);
            foreach ((CardPile expectedPile, CardPile actualPile) in
                     expectedCombat.AllPiles.Zip(actualCombat.AllPiles))
            {
                AssertEquivalentPile(expectedPile, actualPile, actualPlayer, cardMap);
            }

            Assert.Equal(expectedPlayer.Relics.Count, actualPlayer.Relics.Count);
            foreach ((RelicModel expectedRelic, RelicModel actualRelic) in
                     expectedPlayer.Relics.Zip(actualPlayer.Relics))
            {
                Assert.NotSame(expectedRelic, actualRelic);
                Assert.Equal(expectedRelic.GetType(), actualRelic.GetType());
                Assert.Equal(expectedRelic.Id, actualRelic.Id);
                Assert.Equal(expectedRelic.StackCount, actualRelic.StackCount);
                Assert.Same(actualPlayer, actualRelic.Owner);
            }

            Assert.Equal(expectedPlayer.PotionSlots.Count, actualPlayer.PotionSlots.Count);
            foreach ((PotionModel? expectedPotion, PotionModel? actualPotion) in
                     expectedPlayer.PotionSlots.Zip(actualPlayer.PotionSlots))
            {
                if (expectedPotion is null)
                {
                    Assert.Null(actualPotion);
                    continue;
                }

                Assert.NotNull(actualPotion);
                Assert.NotSame(expectedPotion, actualPotion);
                Assert.Equal(expectedPotion.GetType(), actualPotion.GetType());
                Assert.Equal(expectedPotion.Id, actualPotion.Id);
                Assert.Same(actualPlayer, actualPotion.Owner);
            }
        }

        Creature[] expectedCreatures = expected.Creatures.ToArray();
        Creature[] actualCreatures = actual.Creatures.ToArray();
        Assert.Equal(expectedCreatures.Length, actualCreatures.Length);
        var creatureMap = new Dictionary<Creature, Creature>(ReferenceEqualityComparer.Instance);
        for (int creatureIndex = 0; creatureIndex < expectedCreatures.Length; creatureIndex++)
        {
            creatureMap.Add(expectedCreatures[creatureIndex], actualCreatures[creatureIndex]);
        }

        foreach ((Creature expectedCreature, Creature actualCreature) in
                 expectedCreatures.Zip(actualCreatures))
        {
            Assert.NotSame(expectedCreature, actualCreature);
            Assert.Equal(expectedCreature.Side, actualCreature.Side);
            Assert.Equal(expectedCreature.CombatId, actualCreature.CombatId);
            Assert.Equal(expectedCreature.SlotName, actualCreature.SlotName);
            Assert.Equal(expectedCreature.CurrentHp, actualCreature.CurrentHp);
            Assert.Equal(expectedCreature.MaxHp, actualCreature.MaxHp);
            Assert.Equal(expectedCreature.Block, actualCreature.Block);
            Assert.Same(actual, actualCreature.CombatState);
            if (expectedCreature.Player is not null)
            {
                Assert.Same(playerMap[expectedCreature.Player], actualCreature.Player);
                Assert.Same(actualCreature, actualCreature.Player!.Creature);
                Assert.Null(actualCreature.Monster);
            }
            else
            {
                Assert.Null(actualCreature.Player);
                Assert.NotNull(expectedCreature.Monster);
                Assert.NotNull(actualCreature.Monster);
                Assert.NotSame(expectedCreature.Monster, actualCreature.Monster);
                Assert.Equal(expectedCreature.Monster.GetType(), actualCreature.Monster.GetType());
                Assert.Equal(expectedCreature.Monster.Id, actualCreature.Monster.Id);
                Assert.Same(actualCreature, actualCreature.Monster.Creature);
                Assert.Same(actual.RunState.Rng, actualCreature.Monster.RunRng);
                Assert.NotSame(expectedCreature.Monster.Rng, actualCreature.Monster.Rng);
                Assert.Equal(expectedCreature.Monster.Rng.Counter, actualCreature.Monster.Rng.Counter);
                Assert.Equal(expectedCreature.Monster.IsPerformingMove, actualCreature.Monster.IsPerformingMove);
                Assert.Equal(expectedCreature.Monster.SpawnedThisTurn, actualCreature.Monster.SpawnedThisTurn);
                AssertEquivalentMoveRuntime(expectedCreature.Monster, actualCreature.Monster);
            }
        }

        foreach ((Creature expectedCreature, Creature actualCreature) in
                 expectedCreatures.Zip(actualCreatures))
        {
            Assert.Equal(expectedCreature.Powers.Count, actualCreature.Powers.Count);
            foreach ((PowerModel expectedPower, PowerModel actualPower) in
                     expectedCreature.Powers.Zip(actualCreature.Powers))
            {
                Assert.NotSame(expectedPower, actualPower);
                Assert.Equal(expectedPower.GetType(), actualPower.GetType());
                Assert.Equal(expectedPower.Id, actualPower.Id);
                Assert.Equal(expectedPower.Amount, actualPower.Amount);
                Assert.Equal(expectedPower.SkipNextDurationTick, actualPower.SkipNextDurationTick);
                Assert.Same(actualCreature, actualPower.Owner);
                if (expectedPower.Applier is null)
                {
                    Assert.Null(actualPower.Applier);
                }
                else
                {
                    Assert.Same(creatureMap[expectedPower.Applier], actualPower.Applier);
                }

                if (expectedPower is RingingPower expectedRinging &&
                    actualPower is RingingPower actualRinging)
                {
                    foreach ((CardModel expectedCard, CardModel actualCard) in cardMap)
                    {
                        Assert.Equal(
                            expectedRinging.IsRinging(expectedCard),
                            actualRinging.IsRinging(actualCard));
                    }
                }
            }
        }
    }

    private static void AssertEquivalentPile(
        CardPile expected,
        CardPile actual,
        Player actualOwner,
        IDictionary<CardModel, CardModel> cardMap)
    {
        Assert.NotSame(expected, actual);
        Assert.Equal(expected.Type, actual.Type);
        Assert.Equal(expected.Cards.Count, actual.Cards.Count);
        foreach ((CardModel expectedCard, CardModel actualCard) in expected.Cards.Zip(actual.Cards))
        {
            cardMap.Add(expectedCard, actualCard);
            Assert.NotSame(expectedCard, actualCard);
            Assert.Equal(expectedCard.GetType(), actualCard.GetType());
            Assert.Equal(expectedCard.Id, actualCard.Id);
            Assert.Same(actualOwner, actualCard.Owner);
            Assert.Same(actual, actualCard.Pile);
            Assert.Equal(expectedCard.EnergyCost, actualCard.EnergyCost);
            Assert.Equal(expectedCard.StarCost, actualCard.StarCost);
            Assert.Equal(expectedCard.CurrentUpgradeLevel, actualCard.CurrentUpgradeLevel);
            Assert.Equal(expectedCard.BaseReplayCount, actualCard.BaseReplayCount);
            Assert.Equal(expectedCard.TemporaryFreeThisTurn, actualCard.TemporaryFreeThisTurn);
            Assert.Equal(expectedCard.TemporaryFreeUntilPlayed, actualCard.TemporaryFreeUntilPlayed);
            Assert.Equal(expectedCard.TemporaryCostOverrideThisTurn, actualCard.TemporaryCostOverrideThisTurn);
            Assert.Equal(expectedCard.TemporaryCostOverrideThisCombat, actualCard.TemporaryCostOverrideThisCombat);
            Assert.Equal(expectedCard.FloorAddedToDeck, actualCard.FloorAddedToDeck);
            Assert.Equal(
                expectedCard.Keywords.OrderBy(keyword => keyword),
                actualCard.Keywords.OrderBy(keyword => keyword));
            Assert.Equal(
                expectedCard.Tags.OrderBy(tag => tag),
                actualCard.Tags.OrderBy(tag => tag));
            Assert.Equal(expectedCard.Enchantments.Count, actualCard.Enchantments.Count);
            foreach ((EnchantmentModel expectedEnchantment, EnchantmentModel actualEnchantment) in
                     expectedCard.Enchantments.Zip(actualCard.Enchantments))
            {
                Assert.NotSame(expectedEnchantment, actualEnchantment);
                Assert.Equal(expectedEnchantment.GetType(), actualEnchantment.GetType());
                Assert.Equal(expectedEnchantment.Id, actualEnchantment.Id);
                Assert.Equal(expectedEnchantment.Status, actualEnchantment.Status);
                Assert.Equal(expectedEnchantment.Magnitude, actualEnchantment.Magnitude);
                Assert.Same(actualCard, actualEnchantment.Owner);
            }
        }
    }

    private static void AssertEquivalentMoveRuntime(
        MonsterModel expected,
        MonsterModel actual)
    {
        Assert.Equal(expected.NextMove?.StateId, actual.NextMove?.StateId);
        Assert.NotNull(expected.MoveStateMachine);
        Assert.NotNull(actual.MoveStateMachine);
        MonsterMoveStateMachine expectedMachine = expected.MoveStateMachine;
        MonsterMoveStateMachine actualMachine = actual.MoveStateMachine;
        Assert.NotSame(expectedMachine, actualMachine);
        Assert.Equal(
            expectedMachine.States.Keys.OrderBy(id => id),
            actualMachine.States.Keys.OrderBy(id => id));
        Assert.Equal(
            expectedMachine.StateLog.Select(state => state.Id),
            actualMachine.StateLog.Select(state => state.Id));
        foreach ((string id, MonsterState expectedState) in expectedMachine.States)
        {
            MonsterState actualState = actualMachine.States[id];
            Assert.NotSame(expectedState, actualState);
            Assert.Equal(expectedState.GetType(), actualState.GetType());
            Assert.Equal(expectedState.IsMove, actualState.IsMove);
            Assert.Equal(expectedState.CanTransitionAway, actualState.CanTransitionAway);
            Assert.Equal(expectedState.ShouldAppearInLogs, actualState.ShouldAppearInLogs);
        }

        if (expected.NextMove is null)
        {
            Assert.Null(actual.NextMove);
        }
        else
        {
            Assert.NotNull(actual.NextMove);
            Assert.NotSame(expected.NextMove, actual.NextMove);
            Assert.Same(actualMachine.States[actual.NextMove.StateId], actual.NextMove);
        }
    }

    private static async Task<(CombatState State, CombatEngine Engine)> NewStartedCombat(
        string seed,
        Type? monsterType = null)
    {
        var runState = new CloneTestRunState(seed);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var state = new CombatState(runState);
        MonsterModel canonicalMonster = monsterType is null
            ? ModelDb.Monster<TrainingDummy>()
            : (MonsterModel)ModelDb.Get(monsterType);
        state.AddMonster(
            (MonsterModel)canonicalMonster.MutableClone(),
            CombatSide.Enemy);
        var engine = new CombatEngine(state);
        await engine.StartCombatAsync();
        return (state, engine);
    }

    private static void DrawSequentialRngOperations(Rng rng)
    {
        _ = rng.NextInt(1_000_000);
        _ = rng.NextInt(1, 1_000_000);
        _ = rng.NextUnsignedInt(1u, 1_000_000u);
        _ = rng.NextUnsignedLong(1uL, 1_000_000uL);
        _ = rng.NextFloat(-1f, 1f);
        _ = rng.NextDouble(-1d, 1d);
    }

    private static (CardModel? Card, Creature? Target) FirstLegalCard(CombatState state)
    {
        CardModel? card = state.Players[0].PlayerCombatState!.Hand.Cards
            .FirstOrDefault(candidate => candidate.CanPlay(out _));
        Creature? target = card?.TargetType == TargetType.AnyEnemy
            ? state.HittableEnemies.FirstOrDefault()
            : null;
        return (card, target);
    }

    private static string Snapshot(CombatState state)
    {
        Player player = state.Players[0];
        PlayerCombatState playerCombat = player.PlayerCombatState!;
        IEnumerable<string> piles = playerCombat.AllPiles.Select(pile =>
            $"{pile.Type}:{string.Join(',', pile.Cards.Select(card => card.Id.Entry))}");
        IEnumerable<string> runRng = Enum.GetValues<RunRngType>()
            .Select(type => $"{type}:{state.RunState.Rng.GetRng(type).Counter}");
        IEnumerable<string> playerRng = Enum.GetValues<PlayerRngType>()
            .Select(type => $"{type}:{player.PlayerRng.GetRng(type).Counter}");
        IEnumerable<string> enemies = state.Enemies.Select(enemy =>
            $"{enemy.CombatId}:{enemy.CurrentHp}/{enemy.MaxHp}:{enemy.Block}:" +
            $"{enemy.Monster!.NextMove?.StateId}:{enemy.Monster.Rng.Counter}");
        return string.Join('|', new[]
        {
            state.CurrentSide.ToString(),
            state.RoundNumber.ToString(),
            $"P:{player.Creature.CurrentHp}/{player.Creature.MaxHp}:{player.Creature.Block}",
            $"E:{playerCombat.Energy}:{playerCombat.Stars}:{playerCombat.TurnNumber}",
            string.Join(';', piles),
            string.Join(';', enemies),
            string.Join(';', runRng),
            string.Join(';', playerRng),
        });
    }

    private sealed class CloneRuntimeMonster : MonsterModel
    {
        public override int MinInitialHp => 20;

        public override int MaxInitialHp => 20;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var first = new MoveState("FIRST", _ => Task.CompletedTask)
            {
                MustPerformOnceBeforeTransitioning = true,
                FollowUpStateId = "SECOND",
            };
            var second = new MoveState("SECOND", _ => Task.CompletedTask)
            {
                FollowUpStateId = "FIRST",
            };
            return new MonsterMoveStateMachine(new MonsterState[] { first, second }, first);
        }
    }

    private sealed class CloneTransientMonster : MonsterModel
    {
        public override int MinInitialHp => 20;

        public override int MaxInitialHp => 20;

        protected override MonsterMoveStateMachine GenerateMoveStateMachine()
        {
            var first = new MoveState("FIRST", _ => Task.CompletedTask)
            {
                FollowUpStateId = "SECOND",
            };
            var second = new MoveState("SECOND", _ => Task.CompletedTask)
            {
                FollowUpStateId = "FIRST",
            };
            return new MonsterMoveStateMachine(new MonsterState[] { first, second }, first);
        }
    }

    private sealed class CloneTestRunState : IRunState
    {
        private readonly List<Player> _players = new();

        public CloneTestRunState(string seed, bool useSemanticKeys = false)
        {
            Rng = useSemanticKeys ? RunRngSet.CreateKeyed(seed) : new RunRngSet(seed);
        }

        public RunRngSet Rng { get; }

        public AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => _players;
        public int TotalFloor => 0;

        public AbstractRoom? CurrentRoom => null;

        public void AddPlayer(Player player) => _players.Add(player);

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) =>
            childCombatState?.IterateHookListeners() ?? Array.Empty<AbstractModel>();
    }
}
