using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class Task9SpecialCardTests : IDisposable
{
    public Task9SpecialCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(Task9BeforePlayFailureCard),
            typeof(Task9OnPlayFailureCard),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void MetadataAndGenerationGates_MatchAuthoritativeSource()
    {
        Enthralled enthralled = ModelDb.Card<Enthralled>();
        Assert.Equal((CardType.Curse, CardRarity.Curse, TargetType.None, 2, 0),
            (enthralled.Type, enthralled.Rarity, enthralled.TargetType, enthralled.EnergyCost, enthralled.MaxUpgradeLevel));
        Assert.Equal(new[] { CardKeyword.Eternal }, enthralled.Keywords);
        Assert.False(enthralled.CanBeGeneratedByModifiers);
        Assert.True(enthralled.CanBeGeneratedInCombat);

        Normality normality = ModelDb.Card<Normality>();
        Assert.Equal((CardType.Curse, CardRarity.Curse, TargetType.None, -1, 0),
            (normality.Type, normality.Rarity, normality.TargetType, normality.EnergyCost, normality.MaxUpgradeLevel));
        Assert.Equal(new[] { CardKeyword.Unplayable }, normality.Keywords);
        Assert.True(normality.CanBeGeneratedByModifiers);
        Assert.True(normality.CanBeGeneratedInCombat);

        Sts2Sim.Core.Models.Cards.Void voidCard = ModelDb.Card<Sts2Sim.Core.Models.Cards.Void>();
        Assert.Equal((CardType.Status, CardRarity.Status, TargetType.None, -1, 0),
            (voidCard.Type, voidCard.Rarity, voidCard.TargetType, voidCard.EnergyCost, voidCard.MaxUpgradeLevel));
        Assert.Equal(new[] { CardKeyword.Unplayable, CardKeyword.Ethereal }, voidCard.Keywords);

        FranticEscape franticEscape = ModelDb.Card<FranticEscape>();
        Assert.Equal((CardType.Status, CardRarity.Status, TargetType.Self, 1, 0),
            (franticEscape.Type, franticEscape.Rarity, franticEscape.TargetType, franticEscape.EnergyCost, franticEscape.MaxUpgradeLevel));
        Assert.Empty(franticEscape.Keywords);
        Assert.True(franticEscape.CanBeGeneratedByModifiers);
        Assert.False(franticEscape.CanBeGeneratedInCombat);
    }

    [Fact]
    public async Task Enthralled_BlocksManualNonEnthralled_ButPublicAutoPlayStillExecutes()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("task9-enthralled-autoplay");
        _ = AddTo<Enthralled>(player, PileType.Hand);
        DefendRegent manual = AddTo<DefendRegent>(player, PileType.Hand);
        DefendRegent automatic = AddTo<DefendRegent>(player, PileType.Draw, CardPilePosition.Top);
        int blockBefore = player.Creature.Block;

        Assert.False(manual.CanPlay(out UnplayableReason reason));
        Assert.True(reason.HasFlag(UnplayableReason.BlockedByHook));
        await manual.PlayAsync(null);
        Assert.Equal(PileType.Hand, manual.Pile!.Type);
        Assert.Equal(blockBefore, player.Creature.Block);

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, 1);

        Assert.Equal(blockBefore + 5, player.Creature.Block);
        Assert.Equal(PileType.Discard, automatic.Pile!.Type);
    }

    [Fact]
    public async Task Enthralled_AllowsAnyInstance_AndDoesNotAffectOtherOwnerOrWhenOutsideHand()
    {
        (Player owner, Player other, _) = await CreateTwoPlayerCombatAsync("task9-enthralled-guards");
        Enthralled first = AddTo<Enthralled>(owner, PileType.Hand);
        Enthralled second = AddTo<Enthralled>(owner, PileType.Hand);
        DefendRegent otherCard = AddTo<DefendRegent>(other, PileType.Hand);
        owner.PlayerCombatState!.Energy = 3;

        Assert.True(second.CanPlay(out _));
        Assert.True(otherCard.CanPlay(out _));

        CardPileCmd.Add(first, PileType.Discard);
        CardPileCmd.Add(second, PileType.Discard);
        DefendRegent ownerCard = AddTo<DefendRegent>(owner, PileType.Hand);
        Assert.True(ownerCard.CanPlay(out _));
    }

    [Fact]
    public async Task Normality_ReplayCountsEachStartedExecution_AndBlocksNextCardAfterThree()
    {
        (Player player, _) = await CreateCombatAsync("task9-normality-replay");
        _ = AddTo<Normality>(player, PileType.Hand);
        DefendRegent replayed = AddTo<DefendRegent>(player, PileType.Hand);
        replayed.BaseReplayCount = 2;
        player.PlayerCombatState!.Energy = 3;

        await replayed.PlayAsync(null);

        Assert.Equal(3, player.PlayerCombatState.CardPlaysStartedThisTurn);
        DefendRegent fourth = AddTo<DefendRegent>(player, PileType.Hand);
        Assert.False(fourth.CanPlay(out UnplayableReason reason));
        Assert.True(reason.HasFlag(UnplayableReason.BlockedByHook));
    }

    [Fact]
    public async Task Normality_PublicAutoPlayPathAlsoRecordsAStartedPlay()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("task9-normality-autoplay");
        _ = AddTo<Normality>(player, PileType.Hand);
        DefendRegent automatic = AddTo<DefendRegent>(player, PileType.Draw, CardPilePosition.Top);

        await AutoPlayCmd.FromTopOfDrawPile(room.Engine.State, player, 1);

        Assert.Equal(1, player.PlayerCombatState!.CardPlaysStartedThisTurn);
        Assert.Equal(1, player.PlayerCombatState.CardsPlayedThisTurn);
        Assert.Equal(PileType.Discard, automatic.Pile!.Type);
    }

    [Fact]
    public async Task CardPlayStarted_IsRecordedAfterBeforeHookButBeforeOnPlay()
    {
        (Player player, _) = await CreateCombatAsync("task9-normality-start-boundary");
        _ = AddTo<Normality>(player, PileType.Hand);
        Task9BeforePlayFailureCard beforeFailure = AddTo<Task9BeforePlayFailureCard>(player, PileType.Hand);

        await Assert.ThrowsAsync<InvalidOperationException>(() => beforeFailure.PlayAsync(null));
        Assert.Equal(0, player.PlayerCombatState!.CardPlaysStartedThisTurn);

        CardPileCmd.Remove(beforeFailure);
        Task9OnPlayFailureCard onPlayFailure = AddTo<Task9OnPlayFailureCard>(player, PileType.Hand);
        await Assert.ThrowsAsync<InvalidOperationException>(() => onPlayFailure.PlayAsync(null));
        Assert.Equal(1, player.PlayerCombatState.CardPlaysStartedThisTurn);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Void_DrawFromEitherPath_LosesOneEnergy(bool fromHandDraw)
    {
        (Player player, CombatRoom room) = await CreateCombatAsync($"task9-void-{fromHandDraw}");
        ClearHandAndDraw(player);
        Sts2Sim.Core.Models.Cards.Void voidCard = AddTo<Sts2Sim.Core.Models.Cards.Void>(player, PileType.Draw, CardPilePosition.Top);
        player.PlayerCombatState!.Energy = 2;

        await CardPileCmd.Draw(room.Engine.State, 1, player, fromHandDraw);

        Assert.Equal(1, player.PlayerCombatState.Energy);
        Assert.Equal(PileType.Hand, voidCard.Pile!.Type);
    }

    [Fact]
    public async Task Void_OtherCardDoesNothing_AndEnergyClampsAtZero()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("task9-void-clamp");
        ClearHandAndDraw(player);
        Sts2Sim.Core.Models.Cards.Void voidCard = AddTo<Sts2Sim.Core.Models.Cards.Void>(player, PileType.Hand);
        DefendRegent defend = AddTo<DefendRegent>(player, PileType.Draw, CardPilePosition.Top);
        player.PlayerCombatState!.Energy = 0;

        await CardPileCmd.Draw(room.Engine.State, 1, player, fromHandDraw: false);
        Assert.Equal(0, player.PlayerCombatState.Energy);
        await voidCard.AfterCardDrawn(voidCard, fromHandDraw: false);

        Assert.Equal(0, player.PlayerCombatState.Energy);
        Assert.Equal(PileType.Hand, defend.Pile!.Type);
    }

    [Fact]
    public async Task FranticEscape_AdditiveCombatCostStacksPerReplayAndClonePreservesIt()
    {
        (Player player, _) = await CreateCombatAsync("task9-frantic-replay");
        FranticEscape card = AddTo<FranticEscape>(player, PileType.Hand);
        player.PlayerCombatState!.Energy = 10;
        card.BaseReplayCount = 1;

        await card.PlayAsync(player.Creature);

        Assert.Equal(3, card.EnergyCost);
        Assert.Equal(3, ((FranticEscape)card.MutableClone()).EnergyCost);
    }

    [Fact]
    public void AddEnergyCostThisCombat_AppendsAfterExistingFreeAndTurnOverride()
    {
        FranticEscape free = (FranticEscape)ModelDb.Card<FranticEscape>().MutableClone();
        free.MakeTemporaryFreeThisTurn();
        free.AddEnergyCostThisCombat(1);
        Assert.Equal(1, free.EnergyCost);

        FranticEscape overridden = (FranticEscape)ModelDb.Card<FranticEscape>().MutableClone();
        overridden.SetTemporaryCostOverrideThisTurn(7);
        overridden.AddEnergyCostThisCombat(1);
        overridden.AddEnergyCostThisCombat(1);
        Assert.Equal(9, overridden.EnergyCost);
        Assert.Equal(3, ((FranticEscape)overridden.MutableClone()).EnergyCost);
    }

    [Fact]
    public void AscendersBane_RemainsAuthoritativeAndRegistryReflectsImplementedCards()
    {
        AscendersBane bane = ModelDb.Card<AscendersBane>();
        Assert.Equal(new[] { CardKeyword.Eternal, CardKeyword.Unplayable, CardKeyword.Ethereal }, bane.Keywords);
        Assert.Equal(0, bane.MaxUpgradeLevel);
        Assert.False(bane.CanBeGeneratedInCombat);
        Assert.False(bane.CanBeGeneratedByModifiers);

        Type[] cards = ContentRegistry.AllTypes.Where(type => typeof(CardModel).IsAssignableFrom(type)).ToArray();
        // Defect adds its 91-card pool and generated Fuel to the previous 410-card registry.
        Assert.Equal(502, cards.Length);
        Assert.Contains(typeof(Enthralled), cards);
        Assert.Contains(typeof(Normality), cards);
        Assert.Contains(typeof(Sts2Sim.Core.Models.Cards.Void), cards);
        Assert.Contains(typeof(FranticEscape), cards);
        Assert.Contains(typeof(Exterminate), cards);
        Assert.Contains(typeof(Squash), cards);
        Assert.Contains(typeof(Metamorphosis), cards);
        Assert.Contains(typeof(LanternKey), cards);
        Assert.Contains(typeof(SpoilsMap), cards);
        Assert.Contains(typeof(Enlightenment), cards);
        Assert.Contains(typeof(Luminesce), cards);
        Assert.Contains(typeof(Relax), cards);
        Assert.Contains(typeof(MadScience), cards);
        Assert.Contains(typeof(BrightestFlame), cards);
        Assert.Contains(typeof(Apotheosis), cards);
        Assert.Contains(typeof(Whistle), cards);
        Assert.Contains(typeof(Wish), cards);
        Assert.Contains(typeof(Maul), cards);
        Assert.Contains(typeof(Apparition), cards);
    }

    private static T AddTo<T>(Player player, PileType pile, CardPilePosition position = CardPilePosition.Bottom)
        where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pile, position);
        return card;
    }

    private static void ClearHandAndDraw(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.Concat(player.PlayerCombatState.DrawPile.Cards).ToList())
        {
            CardPileCmd.Remove(card);
        }
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }

    private static async Task<(Player first, Player second, CombatRoom room)> CreateTwoPlayerCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player first = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player second = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(first);
        runState.AddPlayer(second);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (first, second, room);
    }
}

file sealed class Task9BeforePlayFailureCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    public override Task BeforeCardPlayed(CardPlay cardPlay) =>
        throw new InvalidOperationException("before-card-play failure");
}

file sealed class Task9OnPlayFailureCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override Task OnPlay(CardPlay cardPlay) =>
        throw new InvalidOperationException("on-play failure");
}
