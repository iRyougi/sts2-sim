namespace Sts2Sim.Core.Tests.Models.Powers;

using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
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
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

[Collection("ModelDb")]
public sealed class TangledPowerTests : IDisposable
{
    private sealed class CardEntryProbePower : PowerModel
    {
        public override PowerType Type => PowerType.Buff;

        public override PowerStackType StackType => PowerStackType.Single;

        public int EntryCount { get; private set; }

        public override Task AfterCardEnteredCombat(CardModel card)
        {
            if (card.Owner == Owner.Player)
            {
                EntryCount++;
            }

            return Task.CompletedTask;
        }
    }

    public TangledPowerTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(DivineRight), typeof(WanderingGrunt), typeof(SovereignBlade), typeof(TangledPower),
            typeof(CardEntryProbePower),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ExistingAttack_UsesSurchargedCostForLegalityAndResourceSpending()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("tangled-existing");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        SovereignBlade attack = AddToHand<SovereignBlade>(player);
        DefendRegent skill = AddToHand<DefendRegent>(player);
        int hpBefore = enemy.CurrentHp;

        await PowerCmd.Apply<TangledPower>(
            room.Engine.State,
            player.Creature,
            2m,
            applier: enemy,
            cardSource: null);

        Assert.Equal(4, attack.EnergyCost);
        Assert.Equal(1, skill.EnergyCost);

        player.PlayerCombatState!.Energy = 3;
        await room.Engine.PlayCardAsync(player, attack, enemy);
        Assert.Equal(hpBefore, enemy.CurrentHp);
        Assert.Equal(PileType.Hand, attack.Pile!.Type);
        Assert.Equal(3, player.PlayerCombatState.Energy);

        player.PlayerCombatState.Energy = 4;
        await room.Engine.PlayCardAsync(player, attack, enemy);
        Assert.Equal(hpBefore - 10, enemy.CurrentHp);
        Assert.Equal(0, player.PlayerCombatState.Energy);
    }

    [Fact]
    public async Task GeneratedAttack_EnteredCombatThroughCardPileCmd_IsSurchargedUntilOwnerTurnEnd()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("tangled-generated");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        TangledPower tangled = (await PowerCmd.Apply<TangledPower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: enemy,
            cardSource: null))!;
        SovereignBlade generatedAttack = CreateOwned<SovereignBlade>(player);
        DefendRegent generatedSkill = CreateOwned<DefendRegent>(player);

        await CardPileCmd.Generate(room.Engine.State, generatedAttack, PileType.Hand);
        await CardPileCmd.Generate(room.Engine.State, generatedSkill, PileType.Hand);

        Assert.Equal(3, generatedAttack.EnergyCost);
        Assert.Equal(1, generatedSkill.EnergyCost);

        await Hook.AfterSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            new[] { player.Creature });

        Assert.DoesNotContain(tangled, player.Creature.Powers);
        Assert.Equal(2, generatedAttack.EnergyCost);
    }

    [Fact]
    public async Task ForgedAttack_EnteringCombatAfterTangled_IsSurcharged()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("tangled-forge-entry");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<TangledPower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: enemy,
            cardSource: null);

        await ForgeCmd.Forge(5m, player, source: null);

        SovereignBlade blade = Assert.Single(
            player.PlayerCombatState!.AllPiles.SelectMany(pile => pile.Cards).OfType<SovereignBlade>());
        Assert.Equal(3, blade.EnergyCost);
    }

    [Fact]
    public async Task AttackCreatedByCombatTransformAfterTangled_IsSurcharged()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("tangled-transform-entry");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        DefendRegent original = AddToHand<DefendRegent>(player);
        await PowerCmd.Apply<TangledPower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: enemy,
            cardSource: null);

        SovereignBlade replacement = await CardCmd.TransformTo<SovereignBlade>(original, player.RunState);

        Assert.Equal(PileType.Hand, replacement.Pile!.Type);
        Assert.Equal(3, replacement.EnergyCost);
    }

    [Fact]
    public async Task MovingExistingCombatCardBetweenPiles_DoesNotReemitCardEnteredCombat()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("card-entry-once");
        CardEntryProbePower probe = (await PowerCmd.Apply<CardEntryProbePower>(
            room.Engine.State,
            player.Creature,
            1m,
            applier: null,
            cardSource: null))!;
        SovereignBlade card = CreateOwned<SovereignBlade>(player);

        await CardPileCmd.Generate(room.Engine.State, card, PileType.Hand);
        CardPileCmd.Add(card, PileType.Discard);
        CardPileCmd.Add(card, PileType.Draw);
        CardPileCmd.Add(card, PileType.Hand);

        Assert.Equal(1, probe.EntryCount);
    }

    [Fact]
    public async Task MutableTangledInstancesAcrossCombats_KeepReferenceSetsIndependent()
    {
        (Player firstPlayer, CombatRoom firstRoom) = await CreateCombatAsync("tangled-clone-first");
        (Player secondPlayer, CombatRoom secondRoom) = await CreateCombatAsync("tangled-clone-second");
        SovereignBlade firstCard = AddToHand<SovereignBlade>(firstPlayer);
        SovereignBlade secondCard = AddToHand<SovereignBlade>(secondPlayer);

        TangledPower first = (await PowerCmd.Apply<TangledPower>(
            firstRoom.Engine.State,
            firstPlayer.Creature,
            1m,
            applier: firstRoom.Engine.State.HittableEnemies.Single(),
            cardSource: null))!;
        TangledPower second = (await PowerCmd.Apply<TangledPower>(
            secondRoom.Engine.State,
            secondPlayer.Creature,
            2m,
            applier: secondRoom.Engine.State.HittableEnemies.Single(),
            cardSource: null))!;

        Assert.Equal(firstCard.Id, secondCard.Id);
        Assert.NotSame(firstCard, secondCard);
        Assert.NotSame(first, second);
        Assert.Equal(3, firstCard.EnergyCost);
        Assert.Equal(4, secondCard.EnergyCost);

        await PowerCmd.Remove(first);

        Assert.Equal(2, firstCard.EnergyCost);
        Assert.Equal(4, secondCard.EnergyCost);
        Assert.Contains(second, secondPlayer.Creature.Powers);
    }

    private static TCard AddToHand<TCard>(Player player) where TCard : CardModel
    {
        TCard card = CreateOwned<TCard>(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static TCard CreateOwned<TCard>(Player player) where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
