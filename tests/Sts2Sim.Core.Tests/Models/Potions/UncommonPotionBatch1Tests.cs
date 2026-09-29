using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Potions;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Potions;

[Collection("ModelDb")]
public sealed class UncommonPotionBatch1Tests : IDisposable
{

    public UncommonPotionBatch1Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(UncommonPotionTestCharacter))
                .Append(typeof(UncommonPotionUpgradeableCard))
                .Append(typeof(UncommonPotionDrawCard))
                .Append(typeof(UncommonPotionPlayProbeCard))
                .Append(typeof(RecordedPlayCountModifierPower)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<string> MetadataCases => new()
    {
        "BlessingOfTheForge",
        "Clarity",
        "CureAll",
        "Duplicator",
        "Fortifier",
        "FyshOil",
        "GamblersBrew",
        "HeartOfIron",
    };

    [Theory]
    [MemberData(nameof(MetadataCases))]
    public void Metadata_IsUncommonCombatOnlyAnyPlayer(string potionName)
    {
        PotionModel potion = GetCanonicalPotion(potionName);

        Assert.Equal(PotionRarity.Uncommon, potion.Rarity);
        Assert.Equal(PotionUsage.CombatOnly, potion.Usage);
        Assert.Equal(TargetType.AnyPlayer, potion.TargetType);
    }

    [Fact]
    public async Task BlessingOfTheForge_UpgradesEveryUpgradableCardInTargetHand()
    {
        (IReadOnlyList<Player> players, _) = await CreateCombatAsync("blessing");
        Player player = players[0];
        UncommonPotionUpgradeableCard[] cards =
        {
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand),
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand),
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand),
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand),
        };
        cards[2].Upgrade();

        await UsePotionAsync("BlessingOfTheForge", player, player.Creature);

        Assert.All(cards, card => Assert.Equal(1, card.CurrentUpgradeLevel));
        Assert.All(cards, card => Assert.False(card.IsUpgradable));
    }

    [Fact]
    public async Task Clarity_DrawsOneNow_ThenAddsOneToExactlyThreeOwnerHandDrawsAndDecays()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("clarity", playerCount: 2);
        Player owner = players[0];
        Player otherPlayer = players[1];
        AddCopiesToPile<UncommonPotionDrawCard>(owner, PileType.Draw, 30);

        await UsePotionAsync("Clarity", owner, owner.Creature);

        Assert.Single(owner.PlayerCombatState!.Hand.Cards);
        PowerModel clarity = Assert.Single(
            owner.Creature.Powers,
            power => power.GetType() == RequireTask7Type("ClarityPower"));
        Assert.Equal(3, clarity.Amount);
        Assert.Equal(5m, Hook.ModifyHandDraw(room.Engine.State, otherPlayer, 5m));

        MoveHandToDiscard(owner);
        for (int remainingAmount = 2; remainingAmount >= 0; remainingAmount--)
        {
            int modifiedDraw = (int)Hook.ModifyHandDraw(room.Engine.State, owner, 5m);
            Assert.Equal(6, modifiedDraw);
            await CardPileCmd.Draw(room.Engine.State, modifiedDraw, owner, fromHandDraw: true);
            Assert.Equal(6, owner.PlayerCombatState.Hand.Cards.Count);

            if (remainingAmount == 2)
            {
                Creature enemy = room.Engine.State.HittableEnemies.Single();
                await Hook.AfterSideTurnStart(
                    room.Engine.State,
                    CombatSide.Enemy,
                    new[] { enemy });
                Assert.Equal(3, clarity.Amount);
            }

            await Hook.AfterSideTurnStart(
                room.Engine.State,
                CombatSide.Player,
                room.Engine.State.Allies);
            Assert.Equal(remainingAmount, clarity.Amount);
            if (remainingAmount > 0)
            {
                Assert.Contains(clarity, owner.Creature.Powers);
                MoveHandToDiscard(owner);
            }
        }

        Assert.DoesNotContain(clarity, owner.Creature.Powers);
        Assert.Equal(5m, Hook.ModifyHandDraw(room.Engine.State, owner, 5m));
    }

    [Fact]
    public async Task CureAll_GainsOneEnergyAndDrawsExactlyTwoCards()
    {
        (IReadOnlyList<Player> players, _) = await CreateCombatAsync("cure-all");
        Player player = players[0];
        AddCopiesToPile<UncommonPotionDrawCard>(player, PileType.Draw, 3);
        player.PlayerCombatState!.Energy = 0;

        await UsePotionAsync("CureAll", player, player.Creature);

        Assert.Equal(1, player.PlayerCombatState.Energy);
        Assert.Equal(2, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Single(player.PlayerCombatState.DrawPile.Cards);
    }

    [Fact]
    public async Task Duplicator_DuplicatesOnlyOwnerNextCard_ConsumesOnce_AndSpendsResourcesOnce()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("duplicator-owner", playerCount: 2);
        Player owner = players[0];
        Player otherPlayer = players[1];
        await UsePotionAsync("Duplicator", owner, owner.Creature);
        PowerModel duplication = Assert.Single(
            owner.Creature.Powers,
            power => power.GetType() == RequireTask7Type("DuplicationPower"));

        UncommonPotionPlayProbeCard otherCard =
            AddToPile<UncommonPotionPlayProbeCard>(otherPlayer, PileType.Hand);
        otherPlayer.PlayerCombatState!.Energy = 3;
        await room.Engine.PlayCardAsync(otherPlayer, otherCard, otherPlayer.Creature);

        Assert.Equal(1, otherCard.ResolutionCount);
        Assert.Equal(1, otherCard.LastPlayCount);
        Assert.Equal(2, otherPlayer.PlayerCombatState.Energy);
        Assert.Equal(1, duplication.Amount);
        Assert.Contains(duplication, owner.Creature.Powers);

        UncommonPotionPlayProbeCard ownerCard =
            AddToPile<UncommonPotionPlayProbeCard>(owner, PileType.Hand);
        owner.PlayerCombatState!.Energy = 3;
        await room.Engine.PlayCardAsync(owner, ownerCard, owner.Creature);

        Assert.Equal(2, ownerCard.ResolutionCount);
        Assert.Equal(2, ownerCard.LastPlayCount);
        Assert.Equal(2, owner.PlayerCombatState.Energy);
        Assert.Equal(1, owner.PlayerCombatState.DiscardPile.Cards.Count(card => card == ownerCard));
        Assert.DoesNotContain(ownerCard, owner.PlayerCombatState.PlayPile.Cards);
        Assert.DoesNotContain(duplication, owner.Creature.Powers);
    }

    [Fact]
    public async Task PlayCountPostModifier_DispatchesRecordedModifierAfterItLeavesCombatHooks()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("play-count-recorded-modifier");
        Player player = players[0];
        RecordedPlayCountModifierPower power = Assert.IsType<RecordedPlayCountModifierPower>(
            await PowerCmd.Apply<RecordedPlayCountModifierPower>(
                room.Engine.State,
                player.Creature,
                1m,
                player.Creature,
                null));
        UncommonPotionPlayProbeCard card =
            AddToPile<UncommonPotionPlayProbeCard>(player, PileType.Hand);
        bool executionFinished = false;
        power.ExecutionFinished += model => executionFinished = ReferenceEquals(model, power);

        int modifiedPlayCount = Hook.ModifyCardPlayCount(
            room.Engine.State,
            card,
            1,
            player.Creature,
            out List<AbstractModel> modifyingModels);
        Assert.Equal(2, modifiedPlayCount);
        Assert.Same(power, Assert.Single(modifyingModels));
        await PowerCmd.Remove(power);
        Assert.DoesNotContain(power, player.Creature.Powers);

        await Hook.AfterModifyingCardPlayCount(room.Engine.State, card, modifyingModels);

        Assert.True(power.PostFoldCallbackReceived);
        Assert.True(executionFinished);
    }

    [Fact]
    public async Task DuplicationPower_UnusedStackExpiresOnlyAtOwnerSideEnd()
    {
        (IReadOnlyList<Player> players, CombatRoom room) =
            await CreateCombatAsync("duplicator-expiry", playerCount: 2);
        Player owner = players[0];
        Player otherPlayer = players[1];
        await UsePotionAsync("Duplicator", owner, owner.Creature);
        PowerModel duplication = Assert.Single(
            owner.Creature.Powers,
            power => power.GetType() == RequireTask7Type("DuplicationPower"));
        Creature enemy = room.Engine.State.HittableEnemies.Single();

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Enemy,
            new[] { enemy });
        Assert.Contains(duplication, owner.Creature.Powers);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            new[] { otherPlayer.Creature });
        Assert.Contains(duplication, owner.Creature.Powers);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            new[] { owner.Creature });
        Assert.DoesNotContain(duplication, owner.Creature.Powers);
    }

    [Fact]
    public async Task Fortifier_GainsTwiceCurrentBlockAsUnpoweredAdditionalBlock()
    {
        (IReadOnlyList<Player> players, CombatRoom room) = await CreateCombatAsync("fortifier");
        Player player = players[0];
        await PowerCmd.Apply<DexterityPower>(
            room.Engine.State,
            player.Creature,
            50m,
            player.Creature,
            null);

        await UsePotionAsync("Fortifier", player, player.Creature);
        Assert.Equal(0, player.Creature.Block);

        player.Creature.GainBlockInternal(4m);
        await UsePotionAsync("Fortifier", player, player.Creature);
        Assert.Equal(12, player.Creature.Block);
    }

    [Fact]
    public async Task FyshOil_AppliesOnePermanentStrengthAndDexterity()
    {
        (IReadOnlyList<Player> players, CombatRoom room) = await CreateCombatAsync("fysh-oil");
        Player player = players[0];

        await UsePotionAsync("FyshOil", player, player.Creature);

        StrengthPower strength = Assert.Single(player.Creature.Powers.OfType<StrengthPower>());
        DexterityPower dexterity = Assert.Single(player.Creature.Powers.OfType<DexterityPower>());
        Assert.Equal(1, strength.Amount);
        Assert.Equal(1, dexterity.Amount);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            new[] { player.Creature });
        Assert.Equal(1, strength.Amount);
        Assert.Equal(1, dexterity.Amount);
    }

    [Fact]
    public async Task DiscardAndDraw_DiscardsSelectedCardsAndDrawsSameRequestedCount()
    {
        (IReadOnlyList<Player> players, _) = await CreateCombatAsync("discard-and-draw");
        Player player = players[0];
        UncommonPotionUpgradeableCard first =
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand);
        UncommonPotionUpgradeableCard second =
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand);
        UncommonPotionDrawCard replacementA =
            AddToPile<UncommonPotionDrawCard>(player, PileType.Draw);
        UncommonPotionDrawCard replacementB =
            AddToPile<UncommonPotionDrawCard>(player, PileType.Draw);

        await InvokeDiscardAndDrawAsync(new CardModel[] { first, second }, 2);

        Assert.Equal(new CardModel[] { replacementA, replacementB }, player.PlayerCombatState!.Hand.Cards);
        Assert.Equal(new CardModel[] { first, second }, player.PlayerCombatState.DiscardPile.Cards);
        Assert.Empty(player.PlayerCombatState.DrawPile.Cards);
    }

    [Fact]
    public async Task DiscardAndDraw_SnapshotsLiveHandBeforeMovingCards()
    {
        (IReadOnlyList<Player> players, _) = await CreateCombatAsync("discard-live-hand");
        Player player = players[0];
        UncommonPotionUpgradeableCard first =
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand);
        UncommonPotionUpgradeableCard second =
            AddToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand);
        UncommonPotionDrawCard replacementA =
            AddToPile<UncommonPotionDrawCard>(player, PileType.Draw);
        UncommonPotionDrawCard replacementB =
            AddToPile<UncommonPotionDrawCard>(player, PileType.Draw);
        IReadOnlyList<CardModel> liveHand = player.PlayerCombatState!.Hand.Cards;

        await InvokeDiscardAndDrawAsync(liveHand, 2);

        Assert.Equal(new CardModel[] { replacementA, replacementB }, player.PlayerCombatState.Hand.Cards);
        Assert.Equal(new CardModel[] { first, second }, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task GamblersBrew_ExplicitZeroSelectionLeavesHandAndDrawPileUnchanged()
    {
        (IReadOnlyList<Player> players, _) = await CreateCombatAsync("gamblers-brew");
        Player player = players[0];
        var selection = new LegacySelectionDecisionSource(chooseZero: true);
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        AddCopiesToPile<UncommonPotionUpgradeableCard>(player, PileType.Hand, 2);
        AddCopiesToPile<UncommonPotionDrawCard>(player, PileType.Draw, 2);
        CardModel[] handBefore = player.PlayerCombatState!.Hand.Cards.ToArray();
        CardModel[] drawBefore = player.PlayerCombatState.DrawPile.Cards.ToArray();

        await UsePotionAsync("GamblersBrew", player, player.Creature);

        Sts2Sim.Core.Combat.CardSelectionRequest request = Assert.Single(selection.Requests);
        Assert.Equal(0, request.MinCount); Assert.Equal(handBefore.Length, request.MaxCount); Assert.False(request.Cancelable);
        Assert.Equal(handBefore, player.PlayerCombatState.Hand.Cards);
        Assert.Equal(drawBefore, player.PlayerCombatState.DrawPile.Cards);
        Assert.Empty(player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task HeartOfIron_AppliesExactlySevenPlating()
    {
        (IReadOnlyList<Player> players, _) = await CreateCombatAsync("heart-of-iron");
        Player player = players[0];

        await UsePotionAsync("HeartOfIron", player, player.Creature);

        Assert.Equal(7, Assert.Single(player.Creature.Powers.OfType<PlatingPower>()).Amount);
    }

    private static PotionModel GetCanonicalPotion(string potionName)
    {
        Type potionType = RequireTask7Type(potionName);
        return ModelDb.GetById<PotionModel>(ModelDb.GetId(potionType));
    }

    private static async Task UsePotionAsync(
        string potionName,
        Player owner,
        Creature target)
    {
        PotionModel potion = owner.AddPotionInternal(GetCanonicalPotion(potionName));
        await PotionCmd.Use(potion, owner, target);
    }

    private static async Task InvokeDiscardAndDrawAsync(
        IReadOnlyList<CardModel> cards,
        int cardsToDraw)
    {
        MethodInfo? method = typeof(CardCmd).GetMethod(
            "DiscardAndDraw",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            new[] { typeof(IReadOnlyList<CardModel>), typeof(int) },
            modifiers: null);
        if (method is null)
        {
            throw new Xunit.Sdk.XunitException(
                "Task 7 method CardCmd.DiscardAndDraw(IReadOnlyList<CardModel>, int) is not implemented.");
        }

        object? result = method.Invoke(null, new object[] { cards, cardsToDraw });
        await Assert.IsAssignableFrom<Task>(result);
    }

    private static Type RequireTask7Type(string name)
    {
        string category = name.EndsWith("Power", StringComparison.Ordinal)
            ? "Powers"
            : "Potions";
        return typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.{category}.{name}")
            ?? throw new Xunit.Sdk.XunitException($"Task 7 model {name} is not implemented.");
    }

    private static TCard AddToPile<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private static void AddCopiesToPile<TCard>(
        Player player,
        PileType pileType,
        int count)
        where TCard : CardModel
    {
        for (int index = 0; index < count; index++)
        {
            AddToPile<TCard>(player, pileType);
        }
    }

    private static void MoveHandToDiscard(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToArray())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }

    private static async Task<(IReadOnlyList<Player> players, CombatRoom room)> CreateCombatAsync(
        string seed,
        int playerCount = 1)
    {
        var runState = new RunState(seed, new Overgrowth());
        var players = new List<Player>();
        for (int index = 0; index < playerCount; index++)
        {
            Player player = Player.CreateForNewRun(
                ModelDb.Character<UncommonPotionTestCharacter>(),
                runState);
            runState.AddPlayer(player);
            players.Add(player);
        }

        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (players, room);
    }
}

file sealed class UncommonPotionTestCharacter : CharacterModel
{
    public override int StartingHp => 75;

    public override int StartingGold => 99;
}

file sealed class UncommonPotionUpgradeableCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;
}

file sealed class UncommonPotionDrawCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 0;
}

file sealed class RecordedPlayCountModifierPower : PowerModel
{
    public override PowerType Type => PowerType.Buff;

    public override PowerStackType StackType => PowerStackType.Counter;

    public bool PostFoldCallbackReceived { get; private set; }

    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        card.Owner.Creature == Owner ? playCount + 1 : playCount;

    public override Task AfterModifyingCardPlayCount(CardModel card)
    {
        PostFoldCallbackReceived = true;
        return Task.CompletedTask;
    }
}

file sealed class UncommonPotionPlayProbeCard : CardModel
{
    public override CardType Type => CardType.Skill;

    public override CardRarity Rarity => CardRarity.Token;

    public override TargetType TargetType => TargetType.Self;

    protected override int CanonicalEnergyCost => 1;

    public int ResolutionCount { get; private set; }

    public int LastPlayCount { get; private set; }

    protected override Task OnPlay(CardPlay cardPlay)
    {
        ResolutionCount++;
        LastPlayCount = cardPlay.PlayCount;
        return Task.CompletedTask;
    }
}
