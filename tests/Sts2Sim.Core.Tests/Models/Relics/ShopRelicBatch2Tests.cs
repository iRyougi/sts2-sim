using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Relics;

file sealed class Task11MinionCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override IReadOnlyCollection<CardTag> CanonicalTags =>
        new[] { CardTag.Minion };
}

file sealed class Task11PlainCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task11FirstCardsSelectionSource : ICardSelectionDecisionSource
{
    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
        Task.FromResult<IReadOnlyList<CardModel>>(
            request.Candidates.Take(request.MaxCount).ToArray());
}

[Collection("ModelDb")]
public sealed class ShopRelicBatch2Tests : IDisposable
{
    public ShopRelicBatch2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes
                .Append(typeof(Task11MinionCard))
                .Append(typeof(Task11PlainCard)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void Task11Relics_RegistersExactlyTheEightShopModels()
    {
        RelicModel[] relics =
        {
            ModelDb.Relic<MembershipCard>(),
            ModelDb.Relic<MysticLighter>(),
            ModelDb.Relic<RingingTriangle>(),
            ModelDb.Relic<ScreamingFlagon>(),
            ModelDb.Relic<SlingOfCourage>(),
            ModelDb.Relic<TheAbacus>(),
            ModelDb.Relic<Toolbox>(),
            ModelDb.Relic<VitruvianMinion>(),
        };

        Assert.Equal(8, relics.Length);
        Assert.Equal(8, relics.Select(relic => relic.GetType()).Distinct().Count());
        Assert.All(relics, relic => Assert.Equal(RelicRarity.Shop, relic.Rarity));
    }

    [Fact]
    public async Task MembershipCard_DynamicallyHalvesPricesAndStacksAfterCourier()
    {
        (RunState runState, Player player) = CreateRun("membership-card");
        await Obtain<TheCourier>(player);
        MerchantRoom room = new();
        await room.Enter(runState);
        int[] cardPrices = room.Inventory.Cards.Select(entry => entry.Price).ToArray();
        int[] relicPrices = room.Inventory.Relics.Select(entry => entry.Price).ToArray();
        int[] potionPrices = room.Inventory.Potions.Select(entry => entry.Price).ToArray();
        int removalPrice = room.Inventory.CardRemoval.Price;

        await Obtain<MembershipCard>(player);

        Assert.Equal(cardPrices.Select(price => (int)(price * 0.5m)),
            room.Inventory.Cards.Select(entry => entry.Price));
        Assert.Equal(relicPrices.Select(price => (int)(price * 0.5m)),
            room.Inventory.Relics.Select(entry => entry.Price));
        Assert.Equal(potionPrices.Select(price => (int)(price * 0.5m)),
            room.Inventory.Potions.Select(entry => entry.Price));
        Assert.Equal((int)(removalPrice * 0.5m), room.Inventory.CardRemoval.Price);
    }

    [Fact]
    public async Task MysticLighter_AddsNineOnlyForOwnedEnchantedPoweredAttacks()
    {
        (RunState runState, Player owner, Player other) = CreateTwoPlayerRun("mystic-lighter");
        await Obtain<MysticLighter>(owner);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];
        // 触发条件是**附魔**而非升级（上游判据 cardSource?.Enchantment == null），见偏离 #300。
        StrikeRegent enchanted = CreateOwned<StrikeRegent>(owner);
        await CardCmd.Enchant<Glam>(enchanted, 1m);
        StrikeRegent normal = CreateOwned<StrikeRegent>(owner);
        StrikeRegent upgradedOnly = CreateOwned<StrikeRegent>(owner);
        upgradedOnly.Upgrade();
        StrikeRegent foreign = CreateOwned<StrikeRegent>(other);
        await CardCmd.Enchant<Glam>(foreign, 1m);

        Assert.Equal(15m, ModifiedDamage(room, enemy, owner.Creature, enchanted, ValueProp.Move));
        Assert.Equal(6m, ModifiedDamage(room, enemy, owner.Creature, normal, ValueProp.Move));

        // 仅升级不再触发——这条正是偏离 #300 修复前后行为的分界。
        Assert.Equal(6m, ModifiedDamage(room, enemy, owner.Creature, upgradedOnly, ValueProp.Move));

        Assert.Equal(6m, ModifiedDamage(
            room, enemy, owner.Creature, enchanted, ValueProp.Move | ValueProp.Unpowered));
        Assert.Equal(6m, ModifiedDamage(room, enemy, other.Creature, foreign, ValueProp.Move));
    }

    [Fact]
    public async Task MysticLighter_AcceptsOwnedEnchantedNonAttackPoweredSourceButRejectsForeignDealer()
    {
        (RunState runState, Player owner, Player other) =
            CreateTwoPlayerRun("mystic-lighter-non-attack");
        await Obtain<MysticLighter>(owner);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];
        Task11PlainCard enchantedSkill = CreateOwned<Task11PlainCard>(owner);
        await CardCmd.Enchant<Glam>(enchantedSkill, 1m);

        Assert.Equal(
            15m,
            ModifiedDamage(
                room, enemy, owner.Creature, enchantedSkill, ValueProp.Move));
        Assert.Equal(
            6m,
            ModifiedDamage(
                room, enemy, other.Creature, enchantedSkill, ValueProp.Move));
    }

    [Fact]
    public async Task RingingTriangle_RetainsOnlyTheOwnersFirstTurnHand()
    {
        (RunState runState, Player player) = CreateRun("ringing-triangle");
        await Obtain<RingingTriangle>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        CardModel firstTurnCard = player.PlayerCombatState!.Hand.Cards[0];

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(2, player.PlayerCombatState.TurnNumber);
        Assert.Equal(PileType.Hand, firstTurnCard.Pile!.Type);

        ClearHand(player);
        Task11PlainCard secondTurnCard = AddToPile<Task11PlainCard>(player, PileType.Hand);
        for (int i = 0; i < 5; i++)
        {
            AddToPile<Task11PlainCard>(player, PileType.Draw);
        }

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(PileType.Discard, secondTurnCard.Pile!.Type);
    }

    [Fact]
    public async Task ScreamingFlagon_DealsTwentyUnpoweredDamageToAllEnemiesOnlyWithEmptyHand()
    {
        (RunState runState, Player player) = CreateRun("screaming-flagon");
        await Obtain<ScreamingFlagon>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        room.Engine.State.AddMonster(
            (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(),
            CombatSide.Enemy);
        await PowerCmd.Apply<StrengthPower>(
            room.Engine.State, player.Creature, 50m, player.Creature, null);
        int[] hpBefore = room.Engine.State.Enemies.Select(enemy => enemy.CurrentHp).ToArray();

        await Hook.BeforeSideTurnEnd(
            room.Engine.State, CombatSide.Player, room.Engine.State.Allies);
        Assert.Equal(hpBefore, room.Engine.State.Enemies.Select(enemy => enemy.CurrentHp));

        ClearHand(player);
        await Hook.BeforeSideTurnEnd(
            room.Engine.State, CombatSide.Player, room.Engine.State.Allies);

        Assert.Equal(
            hpBefore.Select(hp => hp - 20),
            room.Engine.State.Enemies.Select(enemy => enemy.CurrentHp));
    }

    [Fact]
    public async Task SlingOfCourage_GrantsTwoStrengthOnlyOnEliteEntry()
    {
        (RunState normalRun, Player normalPlayer) = CreateRun("sling-normal");
        await Obtain<SlingOfCourage>(normalPlayer);
        CombatRoom normalRoom = CreateCombatRoom();
        await normalRoom.Enter(normalRun);
        Assert.Empty(normalPlayer.Creature.Powers.OfType<StrengthPower>());

        (RunState eliteRun, Player elitePlayer) = CreateRun("sling-elite");
        await Obtain<SlingOfCourage>(elitePlayer);
        CombatRoom eliteRoom = CreateCombatRoom(RoomType.Elite);
        await eliteRoom.Enter(eliteRun);

        Assert.Equal(2, Assert.Single(elitePlayer.Creature.Powers.OfType<StrengthPower>()).Amount);
    }

    [Fact]
    public async Task AfterShuffle_AbacusGrantsSixUnpoweredBlock()
    {
        (RunState runState, Player player) = CreateRun("abacus");
        await Obtain<TheAbacus>(player);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        MoveAllCombatCardsToDiscard(player);
        await PowerCmd.Apply<StrengthPower>(
            room.Engine.State, player.Creature, 50m, player.Creature, null);

        await CardPileCmd.Shuffle(room.Engine.State, player);

        Assert.Equal(6, player.Creature.Block);
    }

    [Fact]
    public async Task AfterShuffle_StratagemMovesExactlyAmountFromDrawToHand()
    {
        (RunState runState, Player player) = CreateRun("stratagem");
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        MoveAllCombatCardsToDiscard(player);
        int totalCards = player.PlayerCombatState!.DiscardPile.Cards.Count;
        await PowerCmd.Apply<StratagemPower>(
            room.Engine.State, player.Creature, 2m, player.Creature, null);
        room.Engine.State.CardSelectionSource = new Task11FirstCardsSelectionSource();

        await CardPileCmd.Shuffle(room.Engine.State, player);

        Assert.Equal(2, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(totalCards - 2, player.PlayerCombatState.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task AfterShuffle_StratagemAddsExactlyAmountAndRedirectsOverflowToDiscard()
    {
        (RunState runState, Player player) = CreateRun("stratagem-near-full-hand");
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        MoveAllCombatCardsToDiscard(player);
        foreach (CardModel card in
                 player.PlayerCombatState!.DiscardPile.Cards.Take(9).ToList())
        {
            CardPileCmd.Add(card, PileType.Hand);
        }

        Assert.Equal(9, player.PlayerCombatState.Hand.Cards.Count);
        AddToPile<Task11PlainCard>(player, PileType.Discard);
        AddToPile<Task11PlainCard>(player, PileType.Discard);
        int cardsToShuffle = player.PlayerCombatState.DiscardPile.Cards.Count;
        await PowerCmd.Apply<StratagemPower>(
            room.Engine.State, player.Creature, 2m, player.Creature, null);
        room.Engine.State.CardSelectionSource = new Task11FirstCardsSelectionSource();

        await CardPileCmd.Shuffle(room.Engine.State, player);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState.Hand.Cards.Count);
        Assert.Equal(cardsToShuffle - 2, player.PlayerCombatState.DrawPile.Cards.Count);
        Assert.Single(player.PlayerCombatState.DiscardPile.Cards);
    }

    [Fact]
    public async Task Toolbox_ChoosesOneOfThreeDistinctColorlessCardsOnFirstTurnOnly()
    {
        (RunState runState, Player player) = CreateRun("toolbox");
        await Obtain<Toolbox>(player);
        CombatRoom room = CreateCombatRoom();
        var selection = new LegacySelectionDecisionSource();
        room.ConfigureCardSelectionSource(selection);

        await room.Enter(runState);

        CardSelectionRequest request = Assert.Single(selection.Requests);
        Assert.Equal(3, request.Candidates.Count);
        Assert.Equal(3, request.Candidates.Select(card => card.Id).Distinct().Count());
        Assert.All(request.Candidates, card => Assert.True(card.IsColorless));
        Assert.Single(player.PlayerCombatState!.Hand.Cards, card => card.IsColorless);
        Assert.Equal(1, player.PlayerCombatState.CardsGeneratedThisCombat);
        var firstTurnRng = runState.Rng.CombatCardGeneration.ToSerializable();

        await room.Engine.EndPlayerTurnAsync();

        Assert.Single(selection.Requests);
        Assert.Equal(firstTurnRng, runState.Rng.CombatCardGeneration.ToSerializable());
        Assert.Equal(1, player.PlayerCombatState.CardsGeneratedThisCombat);
    }

    [Fact]
    public async Task VitruvianMinion_DoublesOwnedMinionDamageAndBlockWithControls()
    {
        (RunState runState, Player owner, Player other) = CreateTwoPlayerRun("vitruvian-minion");
        await Obtain<VitruvianMinion>(owner);
        CombatRoom room = CreateCombatRoom();
        await room.Enter(runState);
        Creature enemy = room.Engine.State.Enemies[0];
        Task11MinionCard minion = CreateOwned<Task11MinionCard>(owner);
        Task11PlainCard plain = CreateOwned<Task11PlainCard>(owner);
        Task11MinionCard foreign = CreateOwned<Task11MinionCard>(other);

        Assert.Equal(12m, ModifiedDamage(room, enemy, owner.Creature, minion, ValueProp.Move));
        Assert.Equal(6m, ModifiedDamage(room, enemy, owner.Creature, plain, ValueProp.Move));
        Assert.Equal(6m, ModifiedDamage(room, enemy, other.Creature, foreign, ValueProp.Move));
        Assert.Equal(10m, ModifiedBlock(room, owner.Creature, minion));
        Assert.Equal(5m, ModifiedBlock(room, owner.Creature, plain));
        Assert.Equal(5m, ModifiedBlock(room, other.Creature, foreign));
    }

    private static decimal ModifiedDamage(
        CombatRoom room,
        Creature target,
        Creature dealer,
        CardModel card,
        ValueProp props) =>
        Hook.ModifyDamage(room.Engine.State, target, dealer, 6m, props, card, null, out _);

    private static decimal ModifiedBlock(CombatRoom room, Creature target, CardModel card) =>
        Hook.ModifyBlock(room.Engine.State, target, 5m, ValueProp.Move, card, null, out _);

    private static Task Obtain<TRelic>(Player player)
        where TRelic : RelicModel =>
        RelicCmd.Obtain(ModelDb.Relic<TRelic>(), player);

    private static TCard AddToPile<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        TCard card = CreateOwned<TCard>(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private static TCard CreateOwned<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }

    private static void MoveAllCombatCardsToDiscard(Player player)
    {
        foreach (CardPile pile in player.PlayerCombatState!.AllPiles)
        {
            foreach (CardModel card in pile.Cards.ToList())
            {
                CardPileCmd.Add(card, PileType.Discard);
            }
        }
    }

    private static CombatRoom CreateCombatRoom(RoomType roomType = RoomType.Monster) =>
        new(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(), roomType);

    private static (RunState RunState, Player Player) CreateRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        return (runState, player);
    }

    private static (RunState RunState, Player Owner, Player Other)
        CreateTwoPlayerRun(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player owner = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        Player other = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(owner);
        runState.AddPlayer(other);
        return (runState, owner, other);
    }
}
