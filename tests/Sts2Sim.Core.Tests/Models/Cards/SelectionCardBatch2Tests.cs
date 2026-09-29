using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class SelectionCardBatch2Tests : IDisposable
{
    public SelectionCardBatch2Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(Glimmer), typeof(SecretWeapon), typeof(SeekerStrike), typeof(ThinkingAhead),
            typeof(Anointed), typeof(Scrawl), typeof(RareTestCard),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Glimmer_Upgrade_DrawsFour_AndMovesOneHandCardToDrawTop()
    {
        (Player player, _) = await CreateCombatAsync("glimmer");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        Glimmer card = AddToHand<Glimmer>(player);
        card.Upgrade();
        int drawPileBefore = player.PlayerCombatState!.DrawPile.Cards.Count;

        await card.PlayAsync(target: null);

        Assert.Equal(PileType.Draw, player.PlayerCombatState!.DrawPile.Cards.First().Pile!.Type);
        Assert.True(player.PlayerCombatState!.DrawPile.Cards.Count <= drawPileBefore);
    }

    [Fact]
    public async Task SecretWeapon_MovesFirstAttackFromDrawPileToHand_UpgradeRemovesExhaust()
    {
        (Player player, _) = await CreateCombatAsync("secret-weapon");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        CardModel attackCard = AddTo<StrikeRegent>(player, PileType.Draw);
        SecretWeapon card = AddToHand<SecretWeapon>(player);
        Assert.True(card.HasKeyword(CardKeyword.Exhaust));

        await card.PlayAsync(target: null);

        Assert.Contains(attackCard, player.PlayerCombatState!.Hand.Cards);

        SecretWeapon upgraded = AddToHand<SecretWeapon>(player);
        upgraded.Upgrade();
        Assert.False(upgraded.HasKeyword(CardKeyword.Exhaust));
    }

    [Fact]
    public async Task ThinkingAhead_DrawsTwo_AndMovesOneHandCardToDrawTop_UpgradeRemovesExhaust()
    {
        (Player player, _) = await CreateCombatAsync("thinking-ahead");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ThinkingAhead card = AddToHand<ThinkingAhead>(player);

        await card.PlayAsync(target: null);

        Assert.Equal(PileType.Draw, player.PlayerCombatState!.DrawPile.Cards.First().Pile!.Type);

        ThinkingAhead upgraded = AddToHand<ThinkingAhead>(player);
        upgraded.Upgrade();
        Assert.False(upgraded.HasKeyword(CardKeyword.Exhaust));
    }

    [Fact]
    public async Task SeekerStrike_Upgrade_DealsDamage_AndMovesADrawPileCardToHand()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("seeker-strike");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        SeekerStrike card = AddToHand<SeekerStrike>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 12, enemy.CurrentHp);
        Assert.Equal(handBefore, player.PlayerCombatState!.Hand.Cards.Count);
    }

    [Fact]
    public async Task Anointed_FillsHandWithRareCards_UpgradeAddsRetain()
    {
        (Player player, _) = await CreateCombatAsync("anointed");
        for (int i = 0; i < 5; i++)
        {
            AddTo<RareTestCard>(player, PileType.Draw);
        }

        Anointed card = AddToHand<Anointed>(player);
        int handBefore = player.PlayerCombatState!.Hand.Cards.Count;

        await card.PlayAsync(target: null);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);
        Assert.True(player.PlayerCombatState!.Hand.Cards.Count > handBefore);

        Anointed upgraded = AddToHand<Anointed>(player);
        Assert.False(upgraded.HasKeyword(CardKeyword.Retain));
        upgraded.Upgrade();
        Assert.True(upgraded.HasKeyword(CardKeyword.Retain));
    }

    [Fact]
    public async Task Scrawl_FillsHandToMax_UpgradeAddsRetain()
    {
        (Player player, _) = await CreateCombatAsync("scrawl");
        Scrawl card = AddToHand<Scrawl>(player);

        await card.PlayAsync(target: null);

        Assert.Equal(CardPile.MaxCardsInHand, player.PlayerCombatState!.Hand.Cards.Count);

        Scrawl upgraded = AddToHand<Scrawl>(player);
        Assert.False(upgraded.HasKeyword(CardKeyword.Retain));
        upgraded.Upgrade();
        Assert.True(upgraded.HasKeyword(CardKeyword.Retain));
    }

    private sealed class RareTestCard : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Rare;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 1;

        protected override Task OnPlay(CardPlay cardPlay) => Task.CompletedTask;
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel =>
        AddTo<TCard>(player, PileType.Hand);

    private static TCard AddTo<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
        return card;
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
}
