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
public sealed class GeneratedCardRepairTask7Tests : IDisposable
{
    public GeneratedCardRepairTask7Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(Task7HighHpMonster),
            typeof(Task7ProbeSkill),
            typeof(Task7PhaseProbeCard),
            typeof(Task7FixedStarProbeCard),
            typeof(Task7XEnergyProbeCard),
            typeof(Task7XStarProbeCard),
            typeof(Task7NoStarProbeCard),
            typeof(Task7BladeTransferPower),
            typeof(Task7OnPlayFailureCard),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();
    [Fact]
    public void Task7Cards_ExposeAllThirtyThreeGeneratedSpecFieldsExactly()
    {
        AssertSpec<Mayhem>(new(
            2, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
            true, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, true, null, null, 0, 0m, 0m, 0m));
        AssertSpec<Monologue>(new(
            0, 0, CardType.Skill, CardRarity.Uncommon, TargetType.Self,
            false, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, false, CardKeyword.Retain, null, 0, 0m, 0m, 0m));
        AssertSpec<PaleBlueDot>(new(
            1, 0, CardType.Power, CardRarity.Uncommon, TargetType.Self,
            false, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, false, null, null, 0, 0m, 0m, 0m));
        AssertSpec<SwordSage>(new(
            2, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
            false, false, false, Array.Empty<CardKeyword>(),
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, true, null, null, 0, 0m, 0m, 0m));
        AssertSpec<VoidForm>(new(
            3, 0, CardType.Power, CardRarity.Rare, TargetType.Self,
            false, false, false, new[] { CardKeyword.Ethereal },
            0m, 1, 0m, 0, 0, 0, 0m, 0m, 0m, 0m, 0m,
            0m, 0m, 0, 0, 0m, 0m, false, null, CardKeyword.Ethereal, 0, 0m, 0m, 0m));
    }


    [Fact]
    public async Task Mayhem_UsesAutoPrePlayAfterHandDrawAndPreservesAutoplayResources()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-mayhem-phase", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        await PowerCmd.Apply<MayhemPower>(room.Engine.State, owner.Creature, 1m, owner.Creature, null);
        for (int i = 0; i < 5; i++)
        {
            AddTo<Task7ProbeSkill>(owner, PileType.Draw);
        }
        Task7PhaseProbeCard probe = AddTo<Task7PhaseProbeCard>(owner, PileType.Draw);
        owner.PlayerCombatState!.Energy = 0;
        await PlayerCmd.LoseStars(999, owner);
        await PlayerCmd.GainStars(2, owner);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(PlayerTurnPhase.AutoPrePlay, probe.PhaseWhenPlayed);
        Assert.NotNull(probe.LastPlay);
        Assert.True(probe.LastPlay.IsAutoPlay);
        Assert.Same(room.Engine.State.Enemies.Single(), probe.TargetWhenPlayed);
        Assert.Equal(new ResourceInfo(0, 2, 0, 1), probe.LastPlay.Resources);
        Assert.Equal(3, owner.PlayerCombatState.Energy);
        Assert.Equal(2, owner.PlayerCombatState.Stars);
        Assert.Equal(PileType.Discard, probe.Pile!.Type);
        Assert.Equal(PlayerTurnPhase.Play, owner.PlayerCombatState.Phase);
    }

    [Fact]
    public async Task Mayhem_EmptyDrawAndDiscardPilesAreANoop()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-mayhem-empty", 1);
        Player owner = players[0];
        ClearCombatPiles(owner);
        await PowerCmd.Apply<MayhemPower>(room.Engine.State, owner.Creature, 1m, owner.Creature, null);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Empty(owner.PlayerCombatState!.PlayPile.Cards);
        Assert.Equal(PlayerTurnPhase.Play, owner.PlayerCombatState.Phase);
    }

    [Fact]
    public async Task Monologue_OnlyLaterOwnerCardsGrantStrengthAndTurnEndRevokesExactlyThatStrength()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-monologue", 2);
        Player owner = players[0];
        Player ally = players[1];
        await PowerCmd.Apply<StrengthPower>(room.Engine.State, owner.Creature, -2m, owner.Creature, null);
        Monologue monologue = AddTo<Monologue>(owner, PileType.Hand);
        monologue.Upgrade();

        await room.Engine.PlayCardAsync(owner, monologue, null);
        Assert.True(monologue.HasKeyword(CardKeyword.Retain));
        Assert.Equal(-2, owner.Creature.Powers.OfType<StrengthPower>().Single().Amount);
        await room.Engine.PlayCardAsync(ally, AddTo<Task7ProbeSkill>(ally, PileType.Hand), null);
        Assert.Equal(-2, owner.Creature.Powers.OfType<StrengthPower>().Single().Amount);
        await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);
        await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);
        Assert.DoesNotContain(owner.Creature.Powers, power => power is StrengthPower);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(-2, owner.Creature.Powers.OfType<StrengthPower>().Single().Amount);
        Assert.DoesNotContain(owner.Creature.Powers, power => power.GetType().Name == "MonologuePower");
    }

    [Fact]
    public async Task Monologue_SeparateCopiesTriggerIndependentlyWithoutNewCopyTriggeringItself()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-monologue-multiple", 1);
        Player owner = players[0];

        await room.Engine.PlayCardAsync(owner, AddTo<Monologue>(owner, PileType.Hand), null);
        await room.Engine.PlayCardAsync(owner, AddTo<Monologue>(owner, PileType.Hand), null);
        Assert.Equal(1, owner.Creature.Powers.OfType<StrengthPower>().Single().Amount);
        Assert.Equal(2, owner.Creature.Powers.Count(power => power.GetType().Name == "MonologuePower"));
        await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);
        Assert.Equal(3, owner.Creature.Powers.OfType<StrengthPower>().Single().Amount);
    }

    [Fact]
    public async Task Monologue_TurnEndStrengthRevocationUsesNormalArtifactInteraction()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-monologue-artifact", 1);
        Player owner = players[0];
        await room.Engine.PlayCardAsync(owner, AddTo<Monologue>(owner, PileType.Hand), null);
        await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);
        await PowerCmd.Apply<ArtifactPower>(
            room.Engine.State, owner.Creature, 1m, owner.Creature, null);

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(1, owner.Creature.Powers.OfType<StrengthPower>().Single().Amount);
        Assert.DoesNotContain(owner.Creature.Powers, power => power is ArtifactPower);
        Assert.DoesNotContain(owner.Creature.Powers, power => power is MonologuePower);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task PaleBlueDot_TriggersOnFifthCompletedOwnerPlayOnceAndResetsAtTurnEnd(
        bool upgraded,
        int drawNextTurn)
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync($"task7-pale-blue-dot-{upgraded}", 2);
        Player owner = players[0];
        Player ally = players[1];
        PaleBlueDot paleBlueDot = AddTo<PaleBlueDot>(owner, PileType.Hand);
        if (upgraded)
        {
            paleBlueDot.Upgrade();
        }

        await room.Engine.PlayCardAsync(owner, paleBlueDot, null); // completed play 1
        await room.Engine.PlayCardAsync(ally, AddTo<Task7ProbeSkill>(ally, PileType.Hand), null);
        for (int i = 0; i < 3; i++)
        {
            await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);
        }
        Assert.DoesNotContain(owner.Creature.Powers, power => power is DrawCardsNextTurnPower);

        Task7ProbeSkill fifth = AddTo<Task7ProbeSkill>(owner, PileType.Hand);
        fifth.BaseReplayCount = 1;
        await room.Engine.PlayCardAsync(owner, fifth, null);

        DrawCardsNextTurnPower draw = Assert.Single(owner.Creature.Powers.OfType<DrawCardsNextTurnPower>());
        Assert.Equal(drawNextTurn, draw.Amount);
        await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);
        Assert.Equal(drawNextTurn, draw.Amount);

        await room.Engine.EndPlayerTurnAsync();
        Assert.DoesNotContain(draw, owner.Creature.Powers);
        PaleBlueDotPower power = Assert.Single(owner.Creature.Powers.OfType<PaleBlueDotPower>());
        for (int i = 0; i < 5; i++)
        {
            await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);
        }
        Assert.Equal(drawNextTurn, Assert.Single(owner.Creature.Powers.OfType<DrawCardsNextTurnPower>()).Amount);
        Assert.Contains(power, owner.Creature.Powers);
    }

    [Fact]
    public async Task SwordSage_GrantsExistingAndEnteringBladesAndRemovalPreservesPreexistingReplay()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-sword-sage", 2);
        Player owner = players[0];
        Player ally = players[1];
        SovereignBlade hand = AddTo<SovereignBlade>(owner, PileType.Hand);
        hand.BaseReplayCount = 2;
        SovereignBlade draw = AddTo<SovereignBlade>(owner, PileType.Draw);
        SovereignBlade allyBlade = AddTo<SovereignBlade>(ally, PileType.Hand);

        await room.Engine.PlayCardAsync(owner, AddTo<SwordSage>(owner, PileType.Hand), null);

        Assert.Equal(3, hand.BaseReplayCount);
        Assert.Equal(1, draw.BaseReplayCount);
        Assert.Equal(0, allyBlade.BaseReplayCount);
        var entering = (SovereignBlade)ModelDb.Card<SovereignBlade>().MutableClone();
        entering.AssignOwner(owner);
        await CardPileCmd.EnterCombat(room.Engine.State, entering, PileType.Exhaust);
        Assert.Equal(1, entering.BaseReplayCount);

        PowerModel power = Assert.Single(owner.Creature.Powers, candidate => candidate.GetType().Name == "SwordSagePower");
        await PowerCmd.Remove(power);

        Assert.Equal(2, hand.BaseReplayCount);
        Assert.Equal(0, draw.BaseReplayCount);
        Assert.Equal(0, entering.BaseReplayCount);
        Assert.Equal(0, allyBlade.BaseReplayCount);
    }

    [Fact]
    public async Task SwordSage_CloneRemapsGrantedCardsSoCloneRemovalDoesNotMutateOriginal()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-sword-sage-clone", 1);
        Player owner = players[0];
        SovereignBlade blade = AddTo<SovereignBlade>(owner, PileType.Draw);
        await room.Engine.PlayCardAsync(owner, AddTo<SwordSage>(owner, PileType.Hand), null);
        Assert.Equal(1, blade.BaseReplayCount);

        CombatState clone = room.Engine.State.Clone();
        SovereignBlade clonedBlade = clone.Players[0].PlayerCombatState!.AllPiles
            .SelectMany(pile => pile.Cards).OfType<SovereignBlade>().Single();
        PowerModel clonedPower = Assert.Single(clone.Allies[0].Powers, candidate => candidate.GetType().Name == "SwordSagePower");
        await PowerCmd.Remove(clonedPower);

        Assert.Equal(0, clonedBlade.BaseReplayCount);
        Assert.Equal(1, blade.BaseReplayCount);
    }

    [Fact]
    public async Task SwordSage_TransferMovesItsGrantFromOldOwnerPowerToNewOwnerPower()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-sword-sage-transfer", 2);
        Player owner = players[0];
        Player ally = players[1];
        owner.PlayerCombatState!.Energy = 10;
        ally.PlayerCombatState!.Energy = 10;
        await room.Engine.PlayCardAsync(owner, AddTo<SwordSage>(owner, PileType.Hand), null);
        await room.Engine.PlayCardAsync(ally, AddTo<SwordSage>(ally, PileType.Hand), null);
        SovereignBlade blade = AddTo<SovereignBlade>(owner, PileType.Hand);
        blade.BaseReplayCount = 2;
        await Hook.AfterCardEnteredCombat(room.Engine.State, blade);
        Assert.Equal(3, blade.BaseReplayCount);
        await PowerCmd.Apply<Task7BladeTransferPower>(
            room.Engine.State, owner.Creature, 1m, owner.Creature, null);

        await room.Engine.PlayCardAsync(owner, blade, room.Engine.State.Enemies.Single());

        Assert.Same(ally, blade.Owner);
        Assert.Equal(PileType.Draw, blade.Pile!.Type);
        Assert.Equal(3, blade.BaseReplayCount);
        PowerModel allySwordSage = Assert.Single(
            ally.Creature.Powers, power => power.GetType().Name == "SwordSagePower");
        await PowerCmd.Remove(allySwordSage);
        Assert.Equal(2, blade.BaseReplayCount);
        PowerModel ownerSwordSage = Assert.Single(
            owner.Creature.Powers, power => power.GetType().Name == "SwordSagePower");
        await PowerCmd.Remove(ownerSwordSage);
        Assert.Equal(2, blade.BaseReplayCount);
    }

    [Fact]
    public async Task VoidForm_EndsTurnAndNextTwoOwnedCardsAreFreeForEnergyAndStars()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-void-form-fixed", 2);
        Player owner = players[0];
        Player ally = players[1];
        VoidForm voidForm = AddTo<VoidForm>(owner, PileType.Hand);
        voidForm.Upgrade();
        Assert.False(voidForm.HasKeyword(CardKeyword.Ethereal));
        await PlayerCmd.GainStars(8, owner);

        await room.Engine.PlayCardAsync(owner, voidForm, null);

        Assert.Equal(2, room.Engine.State.RoundNumber);
        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        Assert.Single(owner.Creature.Powers, power => power.GetType().Name == "VoidFormPower");
        Task7FixedStarProbeCard first = AddTo<Task7FixedStarProbeCard>(owner, PileType.Hand);
        Task7FixedStarProbeCard second = AddTo<Task7FixedStarProbeCard>(owner, PileType.Hand);
        Task7FixedStarProbeCard third = AddTo<Task7FixedStarProbeCard>(owner, PileType.Hand);
        Task7FixedStarProbeCard allyCard = AddTo<Task7FixedStarProbeCard>(ally, PileType.Hand);
        first.BaseReplayCount = 1;
        Assert.Equal((0, 0), (first.EnergyCost, first.StarCost));
        Assert.Equal((2, 2), (allyCard.EnergyCost, allyCard.StarCost));

        await room.Engine.PlayCardAsync(owner, first, null);
        await room.Engine.PlayCardAsync(owner, second, null);
        Assert.Equal((0, 0, 0, 0), (
            first.LastPlay!.Resources.EnergySpent, first.LastPlay.Resources.StarsSpent,
            second.LastPlay!.Resources.EnergySpent, second.LastPlay.Resources.StarsSpent));
        Assert.Equal((2, 2), (third.EnergyCost, third.StarCost));
    }

    [Fact]
    public async Task VoidForm_HandlesXAndNoStarCostsAndAutoplayDoesNotConsumeAFreeSlot()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-void-form-x", 1);
        Player owner = players[0];
        await PlayerCmd.GainStars(6, owner);
        await room.Engine.PlayCardAsync(owner, AddTo<VoidForm>(owner, PileType.Hand), null);

        Task7ProbeSkill autoplay = AddTo<Task7ProbeSkill>(owner, PileType.Draw, CardPilePosition.Top);
        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, owner, 1);
        Assert.NotNull(autoplay.LastPlay);
        Task7XEnergyProbeCard xEnergy = AddTo<Task7XEnergyProbeCard>(owner, PileType.Hand);
        Task7XStarProbeCard xStars = AddTo<Task7XStarProbeCard>(owner, PileType.Hand);
        Task7NoStarProbeCard noStars = AddTo<Task7NoStarProbeCard>(owner, PileType.Hand);

        await room.Engine.PlayCardAsync(owner, xEnergy, null);
        int starsBeforeX = owner.PlayerCombatState!.Stars;
        await room.Engine.PlayCardAsync(owner, xStars, null);

        Assert.Equal(new ResourceInfo(3, 3, 0, 0), xEnergy.LastPlay!.Resources);
        // Native X-star cost returns current stars before VoidForm's star-cost hook.
        Assert.Equal(new ResourceInfo(0, 0, starsBeforeX, starsBeforeX), xStars.LastPlay!.Resources);
        Assert.Equal(0, owner.PlayerCombatState.Stars);
        Assert.Equal(-1, noStars.StarCost);
        Assert.False(noStars.HasStarCost);
        Assert.Equal(2, noStars.EnergyCost);
    }

    [Fact]
    public async Task PaleBlueDot_AutoplayCountsButFailedPlayDoesNotCompleteTheThreshold()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-pale-blue-dot-completion", 1);
        Player owner = players[0];
        await room.Engine.PlayCardAsync(owner, AddTo<PaleBlueDot>(owner, PileType.Hand), null);
        for (int i = 0; i < 3; i++)
        {
            AddTo<Task7ProbeSkill>(owner, PileType.Draw, CardPilePosition.Top);
        }

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, owner, 3);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => room.Engine.PlayCardAsync(owner, AddTo<Task7OnPlayFailureCard>(owner, PileType.Hand), null));
        Assert.DoesNotContain(owner.Creature.Powers, power => power is DrawCardsNextTurnPower);

        await room.Engine.PlayCardAsync(owner, AddTo<Task7ProbeSkill>(owner, PileType.Hand), null);

        Assert.Equal(1, Assert.Single(owner.Creature.Powers.OfType<DrawCardsNextTurnPower>()).Amount);
    }

    [Fact]
    public async Task VoidForm_FailedPlayDoesNotConsumeSlotClonePreservesCountAndNewTurnResets()
    {
        (Player[] players, CombatRoom room) = await CreateCombatAsync("task7-void-form-lifecycle", 1);
        Player owner = players[0];
        await PlayerCmd.GainStars(8, owner);
        await room.Engine.PlayCardAsync(owner, AddTo<VoidForm>(owner, PileType.Hand), null);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => room.Engine.PlayCardAsync(owner, AddTo<Task7OnPlayFailureCard>(owner, PileType.Hand), null));

        Task7FixedStarProbeCard first = AddTo<Task7FixedStarProbeCard>(owner, PileType.Hand);
        await room.Engine.PlayCardAsync(owner, first, null);
        Task7FixedStarProbeCard second = AddTo<Task7FixedStarProbeCard>(owner, PileType.Hand);
        Task7PhaseProbeCard third = AddTo<Task7PhaseProbeCard>(owner, PileType.Hand);

        CombatState clone = room.Engine.State.Clone();
        Player clonedOwner = clone.Players[0];
        Task7FixedStarProbeCard clonedSecond = clonedOwner.PlayerCombatState!.Hand.Cards
            .OfType<Task7FixedStarProbeCard>().Single();
        Task7PhaseProbeCard clonedThird = clonedOwner.PlayerCombatState.Hand.Cards
            .OfType<Task7PhaseProbeCard>().Single();
        Assert.Equal((0, 0), (clonedSecond.EnergyCost, clonedSecond.StarCost));
        await clonedSecond.PlayAsync(null);
        Assert.Equal((2, 1), (clonedThird.EnergyCost, clonedThird.StarCost));

        Assert.Equal((0, 0), (second.EnergyCost, second.StarCost));
        await room.Engine.PlayCardAsync(owner, second, null);
        Assert.Equal((2, 1), (third.EnergyCost, third.StarCost));
        await room.Engine.EndPlayerTurnAsync();
        Task7FixedStarProbeCard nextTurn = AddTo<Task7FixedStarProbeCard>(owner, PileType.Hand);
        Assert.Equal((0, 0), (nextTurn.EnergyCost, nextTurn.StarCost));
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

    private static TCard AddTo<TCard>(
        Player player,
        PileType pileType,
        CardPilePosition position = CardPilePosition.Bottom)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType, position);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).ToList())
        {
            CardPileCmd.Remove(card);
        }
    }

    private static async Task<(Player[] Players, CombatRoom Room)> CreateCombatAsync(string seed, int playerCount)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player[] players = Enumerable.Range(0, playerCount)
            .Select(_ => Player.CreateForNewRun(ModelDb.Character<Regent>(), runState))
            .ToArray();
        foreach (Player player in players)
        {
            runState.AddPlayer(player);
        }

        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<Task7HighHpMonster>().MutableClone());
        await room.Enter(runState);
        return (players, room);
    }
}

file class Task7ProbeSkill : CardModel
{
    public CardPlay? LastPlay { get; protected set; }
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override Task OnPlay(CardPlay cardPlay)
    {
        LastPlay = cardPlay;
        return Task.CompletedTask;
    }
}

file sealed class Task7PhaseProbeCard : Task7ProbeSkill
{
    public PlayerTurnPhase? PhaseWhenPlayed { get; private set; }
    public Creature? TargetWhenPlayed { get; private set; }
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 2;
    protected override int CanonicalStarCost => 1;
    protected override Task OnPlay(CardPlay cardPlay)
    {
        PhaseWhenPlayed = Owner.PlayerCombatState!.Phase;
        TargetWhenPlayed = cardPlay.Target;
        return base.OnPlay(cardPlay);
    }
}

file sealed class Task7FixedStarProbeCard : Task7ProbeSkill
{
    protected override int CanonicalEnergyCost => 2;
    protected override int CanonicalStarCost => 2;
}

file sealed class Task7XEnergyProbeCard : Task7ProbeSkill
{
    protected override int CanonicalEnergyCost => -1;
    protected override bool IsXEnergyCost => true;
}

file sealed class Task7XStarProbeCard : Task7ProbeSkill
{
    protected override int CanonicalStarCost => 0;
    protected override bool IsXStarCost => true;
}

file sealed class Task7NoStarProbeCard : Task7ProbeSkill
{
    protected override int CanonicalEnergyCost => 2;
}

file sealed class Task7OnPlayFailureCard : Task7ProbeSkill
{
    protected override Task OnPlay(CardPlay cardPlay) =>
        throw new InvalidOperationException("task-7 intentional on-play failure");
}

file sealed class Task7BladeTransferPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override CardLocation ModifyCardPlayResultLocation(
        CardModel card,
        bool isAutoPlay,
        ResourceInfo resources,
        CardLocation location)
    {
        if (card is not SovereignBlade || card.Owner != Owner.Player)
        {
            return location;
        }

        Player ally = Owner.CombatState!.Players.Single(player => player != Owner.Player);
        return new CardLocation(ally, PileType.Draw, CardPilePosition.Bottom);
    }
}

file sealed class Task7HighHpMonster : MonsterModel
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
