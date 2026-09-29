using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Byrdpip = Sts2Sim.Core.Models.Relics.Byrdpip;
using PaelsLegionRelic = Sts2Sim.Core.Models.Relics.PaelsLegion;

namespace Sts2Sim.Core.Tests.Models.Relics;

[Collection("ModelDb")]
public sealed class ByrdpipTests : IDisposable
{
    public ByrdpipTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("byrdpip-hatch")]
    [InlineData("byrdpip-obtained-in-combat")]
    [InlineData("paels-legion")]
    [InlineData("targeting")]
    [InlineData("clone")]
    public async Task EventPet_SummonsAndBehavesLikeNative(string scenario)
    {
        (RunState runState, Player player) = CreatePlayer($"09-s2-pet-{scenario}");

        if (scenario == "byrdpip-hatch")
        {
            AddDeckEgg(player);
            await new RestSiteRoom().ResolveAsync(player, new RestSiteDecision.Hatch());
            CombatRoom room = await EnterCombat(runState);

            Assert.Empty(player.Deck.Cards.OfType<ByrdonisEgg>());
            Assert.Single(player.Deck.Cards.OfType<ByrdSwoop>());
            AssertNativePet<global::Sts2Sim.Core.Models.Monsters.Byrdpip>(room, player);
            return;
        }

        if (scenario == "byrdpip-obtained-in-combat")
        {
            CombatRoom room = await EnterCombat(runState);

            await RelicCmd.Obtain(ModelDb.Relic<Byrdpip>(), player);

            AssertNativePet<global::Sts2Sim.Core.Models.Monsters.Byrdpip>(room, player);
            return;
        }

        if (scenario == "paels-legion")
        {
            await RelicCmd.Obtain(ModelDb.Relic<PaelsLegionRelic>(), player);
            CombatRoom room = await EnterCombat(runState);
            AssertNativePet<global::Sts2Sim.Core.Models.Monsters.PaelsLegion>(room, player);
            ClearCombatPiles(player);
            player.PlayerCombatState!.Energy = 99;

            await room.Engine.PlayCardAsync(player, AddToHand<DefendRegent>(player), target: null);
            Assert.Equal(10, player.Creature.Block);
            await room.Engine.PlayCardAsync(player, AddToHand<DefendRegent>(player), target: null);
            Assert.Equal(15, player.Creature.Block);
            return;
        }

        await RelicCmd.Obtain(ModelDb.Relic<Byrdpip>(), player);
        CombatRoom combatRoom = await EnterCombat(runState);
        Creature sourcePet =
            AssertNativePet<global::Sts2Sim.Core.Models.Monsters.Byrdpip>(combatRoom, player);

        if (scenario == "targeting")
        {
            Creature enemy = Assert.Single(combatRoom.Engine.State.Enemies);
            int playerHpBefore = player.Creature.CurrentHp;
            int petHpBefore = sourcePet.CurrentHp;

            await DamageCmd.Attack(1m).FromMonster(enemy.Monster!).Execute();

            Assert.True(player.Creature.CurrentHp < playerHpBefore);
            Assert.Equal(petHpBefore, sourcePet.CurrentHp);
            Assert.Contains(sourcePet, combatRoom.Engine.State.GetOpponentsOf(enemy));
            return;
        }

        Assert.Equal("clone", scenario);
        int sourcePlayerHp = player.Creature.CurrentHp;
        int sourcePetHp = sourcePet.CurrentHp;
        CombatState clone = combatRoom.Engine.State.Clone();
        Player clonedPlayer = Assert.Single(clone.Players);
        Creature clonedPet = Assert.Single(clonedPlayer.PlayerCombatState!.Pets);

        Assert.NotSame(sourcePet, clonedPet);
        Assert.NotSame(sourcePet.Monster, clonedPet.Monster);
        Assert.Same(clonedPlayer, clonedPet.PetOwner);
        Assert.Contains(clonedPet, clone.Allies);

        await clone.Engine!.EndPlayerTurnAsync();

        Assert.Equal(CombatSide.Player, combatRoom.Engine.State.CurrentSide);
        Assert.Equal(sourcePlayerHp, player.Creature.CurrentHp);
        Assert.Equal(sourcePetHp, sourcePet.CurrentHp);
        Assert.Same(player, sourcePet.PetOwner);
    }

    [Fact]
    public void Metadata_MatchesSource()
    {
        Byrdpip relic = ModelDb.Relic<Byrdpip>();

        Assert.Equal(RelicRarity.Event, relic.Rarity);
        Assert.True(relic.AddsPet);
        Assert.True(relic.HasUponPickupEffect);
    }

    [Fact]
    public async Task ResolveHatch_WithoutPersistentEgg_RejectsBeforeMutation()
    {
        (RunState runState, Player player) = CreatePlayer("task12-hatch-invalid");
        CardModel[] deckBefore = player.Deck.Cards.ToArray();
        RelicModel[] relicsBefore = player.Relics.ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RestSiteRoom().ResolveAsync(player, new RestSiteDecision.Hatch()));

        Assert.Equal(deckBefore, player.Deck.Cards);
        Assert.Equal(relicsBefore, player.Relics);
        Assert.False(player.HasEventPet());
    }

    [Fact]
    public async Task ResolveHatch_ObtainsByrdpipAndTransformsEveryPersistentEgg()
    {
        (RunState runState, Player player) = CreatePlayer("task12-hatch-deck");
        AddDeckEgg(player, index: 1);
        AddDeckEgg(player, index: 4);

        await new RestSiteRoom().ResolveAsync(player, new RestSiteDecision.Hatch());

        Byrdpip relic = Assert.Single(player.Relics.OfType<Byrdpip>());
        Assert.Same(player, relic.Owner);
        Assert.Empty(player.Deck.Cards.OfType<ByrdonisEgg>());
        Assert.Equal(2, player.Deck.Cards.OfType<ByrdSwoop>().Count());
        Assert.All(player.Deck.Cards.OfType<ByrdSwoop>(), card =>
        {
            Assert.Same(player, card.Owner);
            Assert.Same(player.Deck, card.Pile);
        });
        Assert.True(player.HasEventPet());
    }

    [Fact]
    public async Task AfterObtained_DuringLiveCombat_TransformsDeckAndCombatEggCopies()
    {
        (RunState runState, Player player) = CreatePlayer("task12-hatch-live");
        AddDeckEgg(player);
        var combatRoom = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await combatRoom.Enter(runState);
        Assert.Single(CombatCards(player).OfType<ByrdonisEgg>());

        await RelicCmd.Obtain(ModelDb.Relic<Byrdpip>(), player);

        Assert.Empty(player.Deck.Cards.OfType<ByrdonisEgg>());
        Assert.Empty(CombatCards(player).OfType<ByrdonisEgg>());
        Assert.Single(player.Deck.Cards.OfType<ByrdSwoop>());
        Assert.Single(CombatCards(player).OfType<ByrdSwoop>());
    }

    [Fact]
    public async Task AfterObtained_AfterCombatEndedBeforeTeardown_DoesNotTransformCombatEggCopy()
    {
        (RunState runState, Player player) = CreatePlayer("task12-hatch-ended");
        AddDeckEgg(player);
        var combatRoom = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await combatRoom.Enter(runState);
        ByrdonisEgg combatEgg = Assert.Single(CombatCards(player).OfType<ByrdonisEgg>());
        combatRoom.Engine.State.Enemies[0].LoseHpInternal(decimal.MaxValue, default);
        Assert.True(combatRoom.Engine.CheckWinCondition());
        Assert.False(combatRoom.Engine.IsInProgress);

        await RelicCmd.Obtain(ModelDb.Relic<Byrdpip>(), player);

        Assert.Single(player.Deck.Cards.OfType<ByrdSwoop>());
        Assert.Same(combatEgg, Assert.Single(CombatCards(player).OfType<ByrdonisEgg>()));
        Assert.Empty(CombatCards(player).OfType<ByrdSwoop>());
    }
    [Fact]
    public async Task AfterObtained_OutsideLiveCombat_DoesNotTransformStaleCombatState()
    {
        (RunState runState, Player player) = CreatePlayer("task12-hatch-stale");
        AddDeckEgg(player);
        player.ResetCombatState();
        var staleEgg = (ByrdonisEgg)ModelDb.Card<ByrdonisEgg>().MutableClone();
        staleEgg.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(staleEgg);

        await RelicCmd.Obtain(ModelDb.Relic<Byrdpip>(), player);

        Assert.Single(player.Deck.Cards.OfType<ByrdSwoop>());
        Assert.Same(staleEgg, Assert.Single(player.PlayerCombatState.Hand.Cards.OfType<ByrdonisEgg>()));
        Assert.Empty(player.PlayerCombatState.Hand.Cards.OfType<ByrdSwoop>());
    }

    private static async Task<CombatRoom> EnterCombat(RunState runState)
    {
        var room = new CombatRoom(() =>
            (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return room;
    }

    private static Creature AssertNativePet<TPet>(CombatRoom room, Player player)
        where TPet : MonsterModel
    {
        Creature pet = Assert.Single(player.PlayerCombatState!.Pets);
        Assert.IsType<TPet>(pet.Monster);
        Assert.Same(player, pet.PetOwner);
        Assert.Equal(CombatSide.Player, pet.Side);
        Assert.Equal(9999, pet.CurrentHp);
        Assert.Equal(9999, pet.MaxHp);
        Assert.Contains(pet, room.Engine.State.Allies);
        Assert.Equal("NOTHING_MOVE", pet.Monster!.MoveStateMachine!.CurrentState.Id);
        return pet;
    }

    private static T AddToHand<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.Card<T>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static void ClearCombatPiles(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.AllPiles
                     .SelectMany(pile => pile.Cards).ToList())
        {
            CardPileCmd.Remove(card);
        }
    }
    private static (RunState RunState, Player Player) CreatePlayer(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static ByrdonisEgg AddDeckEgg(Player player, int index = -1)
    {
        var egg = (ByrdonisEgg)ModelDb.Card<ByrdonisEgg>().MutableClone();
        egg.AssignOwner(player);
        player.Deck.AddInternal(egg, index);
        return egg;
    }

    private static IEnumerable<CardModel> CombatCards(Player player) =>
        player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards);
}
