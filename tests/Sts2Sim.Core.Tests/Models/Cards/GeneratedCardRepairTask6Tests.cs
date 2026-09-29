using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.MonsterMoves;
using Sts2Sim.Core.MonsterMoves.Intents;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class GeneratedCardRepairTask6Tests : IDisposable
{
    public GeneratedCardRepairTask6Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(Task6ProbeA),
            typeof(Task6ProbeB),
            typeof(Task6ProbeC),
            typeof(Task6OwnerColorless),
            typeof(Task6TargetColorless),
            typeof(Task6TargetColorless2),
            typeof(Task6TargetColorless3),
            typeof(Task6OwnerCharacter),
            typeof(Task6TargetCharacter),
            typeof(Task6HighHpMonster),
        }));
    }

    [Fact]
    public void Task6Cards_ExposeAllThirtyThreeGeneratedSpecFieldsExactly()
    {
        AssertSpec<ForegoneConclusion>(new(
            1, 0, CardType.Skill, CardRarity.Rare, TargetType.Self,
            false, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, false, null, null, 0, 0m, 0m, 0m));
        AssertSpec<Largesse>(new(
            0, 0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly,
            false, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, false, null, null, 0, 0m, 0m, 0m));
        AssertSpec<Mimic>(new(
            1, 0, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly,
            true, false, false, new[] { CardKeyword.Exhaust },
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, false, null, CardKeyword.Exhaust, 0, 0m, 0m, 0m));
        AssertSpec<Stratagem>(new(
            1, 0, CardType.Power, CardRarity.Uncommon, TargetType.Self,
            true, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, true, null, null, 0, 0m, 0m, 0m));
        AssertSpec<Tutor>(new(
            1, 0, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly,
            false, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, true, null, null, 0, 0m, 0m, 0m));
        AssertSpec<Tyranny>(new(
            1, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
            false, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, false, CardKeyword.Innate, null, 0, 0m, 0m, 0m));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Task6Cards_PreserveCanonicalMetadataAndUpgradeBranches()
    {
        ForegoneConclusion foregone = Assert.IsType<ForegoneConclusion>(
            ModelDb.Card<ForegoneConclusion>().MutableClone());
        Assert.Equal((1, CardType.Skill, CardRarity.Rare, TargetType.Self),
            (foregone.EnergyCost, foregone.Type, foregone.Rarity, foregone.TargetType));
        foregone.Upgrade();
        Assert.Equal(1, foregone.EnergyCost);

        Largesse largesse = Assert.IsType<Largesse>(ModelDb.Card<Largesse>().MutableClone());
        Assert.Equal((0, CardType.Skill, CardRarity.Uncommon, TargetType.AnyAlly, true),
            (largesse.EnergyCost, largesse.Type, largesse.Rarity, largesse.TargetType,
                largesse.IsMultiplayerOnly));

        Mimic mimic = Assert.IsType<Mimic>(ModelDb.Card<Mimic>().MutableClone());
        Assert.True(mimic.HasKeyword(CardKeyword.Exhaust));
        mimic.Upgrade();
        Assert.False(mimic.HasKeyword(CardKeyword.Exhaust));

        Stratagem stratagem = Assert.IsType<Stratagem>(
            ModelDb.Card<Stratagem>().MutableClone());
        Assert.Equal((1, CardType.Power, CardRarity.Uncommon, TargetType.Self),
            (stratagem.EnergyCost, stratagem.Type, stratagem.Rarity, stratagem.TargetType));
        stratagem.Upgrade();
        Assert.Equal(0, stratagem.EnergyCost);

        Tutor tutor = Assert.IsType<Tutor>(ModelDb.Card<Tutor>().MutableClone());
        Assert.Equal((1, CardType.Skill, CardRarity.Rare, TargetType.AnyAlly, true),
            (tutor.EnergyCost, tutor.Type, tutor.Rarity, tutor.TargetType,
                tutor.IsMultiplayerOnly));
        tutor.Upgrade();
        Assert.Equal(0, tutor.EnergyCost);

        Tyranny tyranny = Assert.IsType<Tyranny>(ModelDb.Card<Tyranny>().MutableClone());
        Assert.Equal((1, CardType.Power, CardRarity.Rare, TargetType.Self),
            (tyranny.EnergyCost, tyranny.Type, tyranny.Rarity, tyranny.TargetType));
        Assert.False(tyranny.HasKeyword(CardKeyword.Innate));
        tyranny.Upgrade();
        Assert.True(tyranny.HasKeyword(CardKeyword.Innate));
    }

    [Fact]
    public async Task ForegoneConclusion_UsesOwnerChoiceBeforeNormalDrawThenConsumesPower()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-foregone", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        ForegoneConclusion card = AddToPile<ForegoneConclusion>(owner, PileType.Hand);
        AddToPile<Task6ProbeA>(owner, PileType.Discard);
        AddToPile<Task6ProbeB>(owner, PileType.Discard);
        AddToPile<Task6ProbeC>(owner, PileType.Discard);
        var selector = new Task6SelectionSource(request => request.Candidates.Skip(1).Take(2));
        room.Engine.State.CardSelectionSource = selector;

        await room.Engine.PlayCardAsync(owner, card, target: null);
        Assert.Equal(2, Assert.Single(owner.Creature.Powers.OfType<ForegoneConclusionPower>()).Amount);
        CardPileCmd.Remove(card);

        await Hook.BeforeHandDraw(room.Engine.State, owner);

        CardSelectionRequest request = Assert.Single(selector.Requests);
        Assert.Same(owner, request.Player);
        Assert.Equal((2, 2), (request.MinCount, request.MaxCount));
        Assert.Equal(3, request.Candidates.Count);
        Assert.DoesNotContain(request.Candidates[0], owner.PlayerCombatState!.Hand.Cards);
        Assert.Contains(request.Candidates[1], owner.PlayerCombatState.Hand.Cards);
        Assert.Contains(request.Candidates[2], owner.PlayerCombatState.Hand.Cards);
        Assert.DoesNotContain(owner.Creature.Powers, power => power is ForegoneConclusionPower);
    }

    [Fact]
    public async Task ForegoneConclusion_TriggersThroughEngineBeforeTheNextNormalHandDraw()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-foregone-engine", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        for (int i = 0; i < 8; i++)
        {
            AddToPile<Task6ProbeA>(owner, PileType.Discard);
        }
        await PowerCmd.Apply<ForegoneConclusionPower>(
            room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        var selector = new Task6SelectionSource(request => request.Candidates.TakeLast(2));
        room.Engine.State.CardSelectionSource = selector;

        await room.Engine.EndPlayerTurnAsync();

        CardSelectionRequest request = Assert.Single(selector.Requests);
        Assert.Equal(8, request.Candidates.Count);
        Assert.Equal((2, 2), (request.MinCount, request.MaxCount));
        Assert.Equal(7, owner.PlayerCombatState!.Hand.Cards.Count);
        Assert.Single(owner.PlayerCombatState.DrawPile.Cards);
        Assert.Contains(request.Candidates[^1], owner.PlayerCombatState.Hand.Cards);
        Assert.Contains(request.Candidates[^2], owner.PlayerCombatState.Hand.Cards);
        Assert.DoesNotContain(
            owner.Creature.Powers,
            power => power is ForegoneConclusionPower);
    }

    [Fact]
    public async Task ForegoneConclusion_UpgradeSelectsThreeAndInsufficientPileSelectsAll()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-foregone-upgrade", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        ForegoneConclusion card = AddToPile<ForegoneConclusion>(owner, PileType.Hand);
        card.Upgrade();
        AddToPile<Task6ProbeA>(owner, PileType.Discard);
        AddToPile<Task6ProbeB>(owner, PileType.Discard);
        room.Engine.State.CardSelectionSource = new Task6SelectionSource(request => request.Candidates);

        await room.Engine.PlayCardAsync(owner, card, target: null);
        Assert.Equal(3, Assert.Single(
            owner.Creature.Powers.OfType<ForegoneConclusionPower>()).Amount);
        CardPileCmd.Remove(card);
        await Hook.BeforeHandDraw(room.Engine.State, owner);

        Assert.Equal(2, owner.PlayerCombatState!.Hand.Cards.Count);
        Assert.Empty(owner.PlayerCombatState.DrawPile.Cards);
        Assert.DoesNotContain(owner.Creature.Powers, power => power is ForegoneConclusionPower);
    }

    [Fact]
    public async Task ForegoneConclusion_RespectsHandCapacityWithoutLeavingSelectedCardInDraw()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-foregone-capacity", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        for (int i = 0; i < 9; i++)
        {
            AddToPile<Task6ProbeA>(owner, PileType.Hand);
        }
        AddToPile<Task6ProbeB>(owner, PileType.Discard);
        AddToPile<Task6ProbeC>(owner, PileType.Discard);
        await PowerCmd.Apply<ForegoneConclusionPower>(
            room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        room.Engine.State.CardSelectionSource =
            new Task6SelectionSource(request => request.Candidates.Take(2));

        await Hook.BeforeHandDraw(room.Engine.State, owner);

        Assert.Equal(CardPile.MaxCardsInHand, owner.PlayerCombatState!.Hand.Cards.Count);
        Assert.Empty(owner.PlayerCombatState.DrawPile.Cards);
        Assert.Single(owner.PlayerCombatState.DiscardPile.Cards);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Largesse_UsesTargetUnlocksOwnerAndPileButSourceCreatorAndFullPoolShuffle(bool upgraded)
    {
        (Player owner, Player target, CombatRoom room) = await CreatePoolCombatAsync(
            $"task6-largesse-{upgraded}");
        ClearCombatPiles(owner);
        ClearCombatPiles(target);
        Largesse card = AddToPile<Largesse>(owner, PileType.Hand);
        if (upgraded)
        {
            card.Upgrade();
        }
        await PowerCmd.Apply<ArsenalPower>(
            room.Engine.State, owner.Creature, 1m, owner.Creature, null);

        await room.Engine.PlayCardAsync(owner, card, target.Creature);

        Assert.Empty(owner.PlayerCombatState!.Hand.Cards);
        CardModel generated = Assert.Single(target.PlayerCombatState!.Hand.Cards);
        Assert.Contains(generated.GetType(), new[]
        {
            typeof(Task6TargetColorless), typeof(Task6TargetColorless2), typeof(Task6TargetColorless3),
        });
        Assert.Same(target, generated.Owner);
        Assert.Equal(upgraded, generated.IsUpgraded);
        Assert.DoesNotContain(target.PlayerCombatState.Hand.Cards, candidate => candidate is Task6OwnerColorless);
        Assert.Equal(0, owner.PlayerCombatState.CardsGeneratedThisCombat);
        Assert.Equal(1, target.PlayerCombatState.CardsGeneratedThisCombat);
        Assert.Equal(2, room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
        Assert.Equal(1, Assert.Single(owner.Creature.Powers.OfType<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task Largesse_FullTargetHandRedirectsGeneratedCardToTargetDiscard()
    {
        (Player owner, Player target, CombatRoom room) = await CreatePoolCombatAsync(
            "task6-largesse-full-target-hand");
        ClearCombatPiles(owner);
        ClearCombatPiles(target);
        Largesse card = AddToPile<Largesse>(owner, PileType.Hand);
        for (int i = 0; i < CardPile.MaxCardsInHand; i++)
        {
            AddToPile<Task6ProbeA>(target, PileType.Hand);
        }
        await PowerCmd.Apply<ArsenalPower>(
            room.Engine.State, owner.Creature, 1m, owner.Creature, null);

        await room.Engine.PlayCardAsync(owner, card, target.Creature);

        Assert.Equal(CardPile.MaxCardsInHand, target.PlayerCombatState!.Hand.Cards.Count);
        CardModel generated = Assert.Single(target.PlayerCombatState.DiscardPile.Cards);
        Assert.Same(target, generated.Owner);
        Assert.Equal(0, owner.PlayerCombatState!.CardsGeneratedThisCombat);
        Assert.Equal(1, target.PlayerCombatState.CardsGeneratedThisCombat);
        Assert.Equal(1, Assert.Single(owner.Creature.Powers.OfType<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task Largesse_SameSeedAndUnlockSnapshotProduceSameCardAndRngConsumption()
    {
        async Task<(Type GeneratedType, int Counter)> GenerateAsync()
        {
            (Player owner, Player target, CombatRoom room) = await CreatePoolCombatAsync(
                "task6-largesse-deterministic");
            ClearCombatPiles(owner);
            ClearCombatPiles(target);
            Largesse card = AddToPile<Largesse>(owner, PileType.Hand);

            await room.Engine.PlayCardAsync(owner, card, target.Creature);

            return (
                Assert.Single(target.PlayerCombatState!.Hand.Cards).GetType(),
                room.Engine.State.RunState.Rng.CombatCardGeneration.Counter);
        }

        (Type firstType, int firstCounter) = await GenerateAsync();
        (Type secondType, int secondCounter) = await GenerateAsync();

        Assert.Equal(firstType, secondType);
        Assert.Equal(firstCounter, secondCounter);
        Assert.Equal(2, firstCounter);
    }

    [Theory]
    [InlineData(false, PileType.Exhaust)]
    [InlineData(true, PileType.Discard)]
    public async Task Mimic_CopiesTargetsCurrentBlockToOwnerAndUpgradeRemovesExhaust(
        bool upgraded,
        PileType expectedResultPile)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-mimic", 2);
        Player owner = players[0];
        Player target = players[1];
        ClearCombatPiles(owner);
        ClearCombatPiles(target);
        target.Creature.GainBlockInternal(13m);
        Mimic card = AddToPile<Mimic>(owner, PileType.Hand);
        if (upgraded)
        {
            card.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, card, target.Creature);

        Assert.Equal(13, owner.Creature.Block);
        Assert.Equal(13, target.Creature.Block);
        Assert.Equal(expectedResultPile, card.Pile!.Type);
    }

    [Fact]
    public async Task Stratagem_RequiresExactAmountAndRejectsCancelOrShortSelection()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-stratagem", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        AddToPile<Task6ProbeA>(owner, PileType.Discard);
        AddToPile<Task6ProbeB>(owner, PileType.Discard);
        AddToPile<Task6ProbeC>(owner, PileType.Discard);
        await PowerCmd.Apply<StratagemPower>(
            room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        var selector = new Task6SelectionSource(_ => Array.Empty<CardModel>());
        room.Engine.State.CardSelectionSource = selector;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CardPileCmd.Shuffle(room.Engine.State, owner));

        CardSelectionRequest request = Assert.Single(selector.Requests);
        Assert.Equal((2, 2), (request.MinCount, request.MaxCount));

        room.Engine.State.CardSelectionSource =
            new Task6SelectionSource(selection => selection.Candidates.Take(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Hook.AfterShuffle(room.Engine.State, owner));
    }

    [Fact]
    public async Task Stratagem_SelectsExactAmountAndLetsSecondCardOverflowToDiscard()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-stratagem-capacity", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        for (int i = 0; i < 9; i++)
        {
            AddToPile<Task6ProbeA>(owner, PileType.Hand);
        }
        AddToPile<Task6ProbeB>(owner, PileType.Discard);
        AddToPile<Task6ProbeC>(owner, PileType.Discard);
        AddToPile<Task6ProbeA>(owner, PileType.Discard);
        await PowerCmd.Apply<StratagemPower>(
            room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        room.Engine.State.CardSelectionSource =
            new Task6SelectionSource(request => request.Candidates.Take(request.MaxCount));

        await CardPileCmd.Shuffle(room.Engine.State, owner);

        Assert.Equal(CardPile.MaxCardsInHand, owner.PlayerCombatState!.Hand.Cards.Count);
        Assert.Single(owner.PlayerCombatState.DiscardPile.Cards);
        Assert.Single(owner.PlayerCombatState.DrawPile.Cards);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Stratagem_InsufficientCandidatesForceAllWithoutExternalRequest(int candidateCount)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync($"task6-stratagem-short-{candidateCount}", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        for (int i = 0; i < candidateCount; i++)
        {
            AddToPile<Task6ProbeA>(owner, PileType.Discard);
        }
        await PowerCmd.Apply<StratagemPower>(
            room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        var selector = new Task6SelectionSource(_ => throw new Xunit.Sdk.XunitException("forced selection requested input"));
        room.Engine.State.CardSelectionSource = selector;

        await CardPileCmd.Shuffle(room.Engine.State, owner);

        Assert.Empty(selector.Requests);
        Assert.Equal(candidateCount, owner.PlayerCombatState!.Hand.Cards.Count);
    }

    [Fact]
    public async Task Tutor_TargetChoosesNonFirstDrawCardIntoTheirOwnHandAndUpgradeReducesCost()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-tutor", 2);
        Player owner = players[0];
        Player target = players[1];
        ClearCombatPiles(owner);
        ClearCombatPiles(target);
        Tutor tutor = AddToPile<Tutor>(owner, PileType.Hand);
        tutor.Upgrade();
        Task6ProbeA first = AddToPile<Task6ProbeA>(target, PileType.Draw);
        Task6ProbeB chosen = AddToPile<Task6ProbeB>(target, PileType.Draw);
        AddToPile<Task6ProbeC>(target, PileType.Draw);
        var selector = new Task6SelectionSource(request => new[] { request.Candidates[1] });
        room.Engine.State.CardSelectionSource = selector;

        Assert.Equal(0, tutor.EnergyCost);
        await room.Engine.PlayCardAsync(owner, tutor, target.Creature);

        CardSelectionRequest request = Assert.Single(selector.Requests);
        Assert.Same(target, request.Player);
        Assert.Equal((1, 1), (request.MinCount, request.MaxCount));
        Assert.Same(chosen, Assert.Single(target.PlayerCombatState!.Hand.Cards));
        Assert.Contains(first, target.PlayerCombatState.DrawPile.Cards);
        Assert.Empty(owner.PlayerCombatState!.Hand.Cards);
    }

    [Fact]
    public async Task Tutor_FullTargetHandMovesChosenCardToTargetDiscard()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-tutor-full", 2);
        Player owner = players[0];
        Player target = players[1];
        ClearCombatPiles(owner);
        ClearCombatPiles(target);
        Tutor tutor = AddToPile<Tutor>(owner, PileType.Hand);
        for (int i = 0; i < CardPile.MaxCardsInHand; i++)
        {
            AddToPile<Task6ProbeA>(target, PileType.Hand);
        }
        Task6ProbeB chosen = AddToPile<Task6ProbeB>(target, PileType.Draw);
        AddToPile<Task6ProbeC>(target, PileType.Draw);
        room.Engine.State.CardSelectionSource =
            new Task6SelectionSource(request => new[] { request.Candidates[0] });

        await room.Engine.PlayCardAsync(owner, tutor, target.Creature);

        Assert.Equal(CardPile.MaxCardsInHand, target.PlayerCombatState!.Hand.Cards.Count);
        Assert.Same(chosen, Assert.Single(target.PlayerCombatState.DiscardPile.Cards));
        Assert.DoesNotContain(chosen, target.PlayerCombatState.DrawPile.Cards);
    }

    [Fact]
    public async Task Tyranny_DrawsExtraThenOwnerChoosesNonFirstCardsToExhaustAtTurnStart()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-tyranny", 2);
        Player owner = players[0];
        Player ally = players[1];
        ClearCombatPiles(owner);
        ClearCombatPiles(ally);
        for (int i = 0; i < 7; i++)
        {
            AddToPile<Task6ProbeA>(owner, PileType.Draw);
        }
        for (int i = 0; i < 5; i++)
        {
            AddToPile<Task6ProbeB>(ally, PileType.Draw);
        }
        await PowerCmd.Apply<TyrannyPower>(
            room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        var selector = new Task6SelectionSource(request => request.Candidates.Skip(1).Take(2));
        room.Engine.State.CardSelectionSource = selector;

        await room.Engine.EndPlayerTurnAsync();

        CardSelectionRequest request = Assert.Single(selector.Requests);
        Assert.Same(owner, request.Player);
        Assert.Equal(7, request.Candidates.Count);
        Assert.Equal((2, 2), (request.MinCount, request.MaxCount));
        Assert.Equal(5, owner.PlayerCombatState!.Hand.Cards.Count);
        Assert.Equal(2, owner.PlayerCombatState.ExhaustPile.Cards.Count);
        Assert.Equal(5, ally.PlayerCombatState!.Hand.Cards.Count);
    }

    [Fact]
    public async Task Tyranny_WithFewerCardsSelectsAllAndIgnoresForeignPlayerTurnHook()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-tyranny-fewer", 2);
        Player owner = players[0];
        Player ally = players[1];
        ClearCombatPiles(owner);
        ClearCombatPiles(ally);
        Task6ProbeA only = AddToPile<Task6ProbeA>(owner, PileType.Hand);
        await PowerCmd.Apply<TyrannyPower>(
            room.Engine.State, owner.Creature, 2m, owner.Creature, null);
        room.Engine.State.CardSelectionSource =
            new Task6SelectionSource(request => request.Candidates);

        await Hook.AfterPlayerTurnStart(room.Engine.State, ally);
        Assert.Same(only, Assert.Single(owner.PlayerCombatState!.Hand.Cards));

        await Hook.AfterPlayerTurnStart(room.Engine.State, owner);
        Assert.Empty(owner.PlayerCombatState.Hand.Cards);
        Assert.Same(only, Assert.Single(owner.PlayerCombatState.ExhaustPile.Cards));
    }

    [Fact]
    public async Task CardSelection_RejectsDuplicateOrForeignCardsAndCloneDoesNotShareMutableSource()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task6-selection-validation", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        Task6ProbeA first = AddToPile<Task6ProbeA>(owner, PileType.Draw);
        Task6ProbeB second = AddToPile<Task6ProbeB>(owner, PileType.Draw);
        var duplicate = new Task6SelectionSource(_ => new CardModel[] { first, first });
        room.Engine.State.CardSelectionSource = duplicate;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CardSelectCmd.SelectCardsAsync(
            room.Engine.State, owner, new CardModel[] { first, second }, 1, 2, source: null));

        Task6ProbeC foreign = CreateCard<Task6ProbeC>(owner);
        var outside = new Task6SelectionSource(_ => new CardModel[] { foreign });
        room.Engine.State.CardSelectionSource = outside;
        await Assert.ThrowsAsync<InvalidOperationException>(() => CardSelectCmd.SelectCardsAsync(
            room.Engine.State, owner, new CardModel[] { first, second }, 1, 2, source: null));

        CombatState clone = room.Engine.State.Clone();
        Assert.NotSame(outside, clone.CardSelectionSource);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    public async Task CardSelection_ForcedCasesReturnWithoutCallingSource(int candidateCount, int minimum)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync($"task6-forced-{candidateCount}-{minimum}", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        var candidates = new List<CardModel>();
        if (candidateCount == 1)
        {
            candidates.Add(AddToPile<Task6ProbeA>(owner, PileType.Draw));
        }
        var source = new Task6SelectionSource(_ => throw new Xunit.Sdk.XunitException("forced case called source"));
        room.Engine.State.CardSelectionSource = source;

        IReadOnlyList<CardModel> selected = await CardSelectCmd.SelectCardsAsync(
            room.Engine.State, owner, candidates, minimum, minimum, source: null);

        Assert.Equal(candidateCount, selected.Count);
        Assert.Empty(source.Requests);
    }

    private static void AssertSpec<TCard>(GeneratedCardSpec expected)
        where TCard : GeneratedCardModel
    {
        var property = typeof(TCard).GetProperty(
            "Spec",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public);
        GeneratedCardSpec actual = Assert.IsType<GeneratedCardSpec>(
            property!.GetValue(ModelDb.Card<TCard>()));
        Assert.Equivalent(expected, actual, strict: true);
    }

    private static TCard CreateCard<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static TCard AddToPile<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        TCard card = CreateCard<TCard>(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToArray())
            {
                CardPileCmd.Remove(card);
            }
        }
    }

    private static async Task<(Player[] Players, CombatRoom Room)> CreateCombatAsync(
        string seed,
        int playerCount)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player[] players = Enumerable.Range(0, playerCount)
            .Select(_ => Player.CreateForNewRun(ModelDb.Character<Regent>(), runState))
            .ToArray();
        foreach (Player player in players)
        {
            runState.AddPlayer(player);
        }

        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<Task6HighHpMonster>().MutableClone());
        await room.Enter(runState);
        return (players, room);
    }

    private static async Task<(Player Owner, Player Target, CombatRoom Room)> CreatePoolCombatAsync(
        string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player owner = Player.CreateForNewRun(
            ModelDb.Character<Task6OwnerCharacter>(),
            runState,
            new PlayerUnlockState(new[] { ModelDb.GetId(typeof(Task6OwnerColorless)) }));
        Player target = Player.CreateForNewRun(
            ModelDb.Character<Task6TargetCharacter>(),
            runState,
            new PlayerUnlockState(new[]
            {
                ModelDb.GetId(typeof(Task6TargetColorless)),
                ModelDb.GetId(typeof(Task6TargetColorless2)),
                ModelDb.GetId(typeof(Task6TargetColorless3)),
            }));
        runState.AddPlayer(owner);
        runState.AddPlayer(target);
        var room = new CombatRoom(
            () => (MonsterModel)ModelDb.Monster<Task6HighHpMonster>().MutableClone());
        await room.Enter(runState);
        return (owner, target, room);
    }
}

file sealed class Task6SelectionSource(
    Func<CardSelectionRequest, IEnumerable<CardModel>> select) : ICardSelectionDecisionSource
{
    public List<CardSelectionRequest> Requests { get; } = new();

    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request)
    {
        Requests.Add(request);
        return Task.FromResult<IReadOnlyList<CardModel>>(select(request).ToList());
    }
}

file abstract class Task6CardBase : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Common;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task6ProbeA : Task6CardBase;
file sealed class Task6ProbeB : Task6CardBase;
file sealed class Task6ProbeC : Task6CardBase;

file sealed class Task6OwnerColorless : Task6CardBase
{
    public override bool IsColorless => true;
}

file sealed class Task6TargetColorless : Task6CardBase
{
    public override bool IsColorless => true;
}

file sealed class Task6TargetColorless2 : Task6CardBase
{
    public override bool IsColorless => true;
}

file sealed class Task6TargetColorless3 : Task6CardBase
{
    public override bool IsColorless => true;
}

file sealed class Task6OwnerCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 0;
}

file sealed class Task6TargetCharacter : CharacterModel
{
    public override int StartingHp => 75;
    public override int StartingGold => 0;
}

file sealed class Task6HighHpMonster : MonsterModel
{
    public override int MinInitialHp => 500;
    public override int MaxInitialHp => 500;

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        var move = new MoveState("WAIT", _ => Task.CompletedTask, new SingleAttackIntent(0));
        move.FollowUpState = move;
        return new MonsterMoveStateMachine(new[] { move }, move);
    }
}
