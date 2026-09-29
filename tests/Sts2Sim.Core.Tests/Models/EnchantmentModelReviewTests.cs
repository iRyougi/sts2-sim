using System.Reflection;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class EnchantmentModelReviewTests : IDisposable
{
    public EnchantmentModelReviewTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(RegisteredEnchantment),
            typeof(DifferentEnchantment),
            typeof(DrawCountingEnchantment),
            typeof(RejectsAllEnchantment),
            typeof(PermissiveEnchantment),
            typeof(StackableEnchantment),
            typeof(GlamStyleEnchantment),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task CombatState_EnumeratesAttachedEnchantmentImmediatelyAfterItsCard()
    {
        (Player player, CombatState combatState) = CreateCombatState("enchantment-listener-order");
        CardModel card = CreateCard<StrikeRegent>(player, PileType.Hand);
        await CardCmd.Enchant<RegisteredEnchantment>(card, 1m);

        AbstractModel[] listeners = combatState.IterateHookListeners().ToArray();
        int cardIndex = Array.IndexOf(listeners, card);

        Assert.True(cardIndex >= 0);
        Assert.Same(card.Enchantments.Single(), listeners[cardIndex + 1]);
    }

    [Fact]
    public async Task Draw_InvokesAttachedEnchantmentExactlyOnceThroughCombatHooks()
    {
        (Player player, CombatState combatState) = CreateCombatState("enchantment-draw-hook");
        CardModel card = CreateCard<StrikeRegent>(player, PileType.Draw);
        await CardCmd.Enchant<DrawCountingEnchantment>(card, 1m);
        var enchantment = Assert.IsType<DrawCountingEnchantment>(card.Enchantments.Single());

        await CardPileCmd.Draw(combatState, 1, player, fromHandDraw: false);

        Assert.Equal(1, enchantment.DrawCount);
        Assert.Same(player.PlayerCombatState!.Hand, card.Pile);
    }

    [Fact]
    public void AttachEnchantment_RejectsAnAlreadyAttachedInstance()
    {
        var firstCard = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        var secondCard = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        var enchantment = (RegisteredEnchantment)ModelDb
            .Get(typeof(RegisteredEnchantment))
            .MutableClone();
        AttachEnchantment(firstCard, enchantment);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => AttachEnchantment(secondCard, enchantment));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Same(firstCard, enchantment.Owner);
        Assert.Single(firstCard.Enchantments);
        Assert.Empty(secondCard.Enchantments);
    }

    [Fact]
    public async Task Enchant_RejectsRepeatedNonStackableType()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        await CardCmd.Enchant<RegisteredEnchantment>(card, 2m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.Enchant<RegisteredEnchantment>(card, 3m));

        Assert.Equal(2m, card.Enchantments.Single().Magnitude);
        Assert.Single(card.Enchantments);
    }

    [Fact]
    public async Task Enchant_RejectsDifferentTypeWhenCardIsAlreadyEnchanted()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        await CardCmd.Enchant<RegisteredEnchantment>(card, 2m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.Enchant<DifferentEnchantment>(card, 3m));

        Assert.IsType<RegisteredEnchantment>(card.Enchantments.Single());
    }

    [Fact]
    public async Task Enchant_PermissiveEligibilityStillRejectsDifferentExistingType()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        await CardCmd.Enchant<RegisteredEnchantment>(card, 2m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.Enchant<PermissiveEnchantment>(card, 3m));

        EnchantmentModel existing = Assert.Single(card.Enchantments);
        Assert.IsType<RegisteredEnchantment>(existing);
        Assert.Equal(2m, existing.Magnitude);
    }

    [Fact]
    public async Task Enchant_PermissiveEligibilityStillRejectsRepeatedNonStackableType()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        await CardCmd.Enchant<PermissiveEnchantment>(card, 2m);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.Enchant<PermissiveEnchantment>(card, 3m));

        EnchantmentModel existing = Assert.Single(card.Enchantments);
        Assert.IsType<PermissiveEnchantment>(existing);
        Assert.Equal(2m, existing.Magnitude);
    }

    [Fact]
    public async Task Enchant_RejectsCardWhenEnchantmentEligibilityRejectsIt()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.Enchant<RejectsAllEnchantment>(card, 1m));

        Assert.Empty(card.Enchantments);
    }

    [Theory]
    [InlineData(CardType.Status)]
    [InlineData(CardType.Curse)]
    [InlineData(CardType.Quest)]
    public async Task Enchant_BaseEligibilityRejectsNonPlayableCardTypes(CardType cardType)
    {
        var card = (TypedCard)new TypedCard(cardType).MutableClone();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CardCmd.Enchant<RegisteredEnchantment>(card, 1m));

        Assert.Empty(card.Enchantments);
    }

    [Fact]
    public async Task Enchant_RepeatedStackableTypeStacksMagnitudeOnSingleInstance()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        await CardCmd.Enchant<StackableEnchantment>(card, 2m);

        await CardCmd.Enchant<StackableEnchantment>(card, 3m);

        EnchantmentModel enchantment = Assert.Single(card.Enchantments);
        Assert.IsType<StackableEnchantment>(enchantment);
        Assert.Equal(5m, enchantment.Magnitude);
    }

    [Fact]
    public async Task PlayAsync_AppliesEnchantmentReplayBeforeGlobalModifiersThenDisables()
    {
        (Player player, _) = CreateCombatState("enchantment-glam-replay");
        player.PlayerCombatState!.Energy = 10;
        var card = (CountingPlayCard)new CountingPlayCard().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        var modifier = (DoublingModifierCard)new DoublingModifierCard().MutableClone();
        modifier.AssignOwner(player);
        CardPileCmd.Add(modifier, PileType.Hand);
        await CardCmd.Enchant<GlamStyleEnchantment>(card, 1m);
        var enchantment = Assert.IsType<GlamStyleEnchantment>(card.Enchantments.Single());

        await card.PlayAsync(target: null);

        Assert.Equal(4, card.TimesPlayed);
        Assert.Equal(EnchantmentStatus.Disabled, enchantment.Status);

        CardPileCmd.Add(card, PileType.Hand);
        await card.PlayAsync(target: null);

        Assert.Equal(6, card.TimesPlayed);
        Assert.Equal(EnchantmentStatus.Disabled, enchantment.Status);
    }

    private static (Player player, CombatState combatState) CreateCombatState(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        return (player, combatState);
    }

    private static TCard CreateCard<TCard>(Player player, PileType pileType)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, pileType);
        return card;
    }

    private static void AttachEnchantment(CardModel card, EnchantmentModel enchantment)
    {
        MethodInfo attach = typeof(CardModel).GetMethod(
            "AttachEnchantment",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("AttachEnchantment was not found.");
        attach.Invoke(card, new object[] { enchantment });
    }

    public sealed class RegisteredEnchantment : EnchantmentModel
    {
    }

    public sealed class DifferentEnchantment : EnchantmentModel
    {
    }

    public sealed class DrawCountingEnchantment : EnchantmentModel
    {
        public int DrawCount { get; private set; }

        public override Task OnDrawn(CardModel card)
        {
            DrawCount++;
            return Task.CompletedTask;
        }
    }

    public sealed class RejectsAllEnchantment : EnchantmentModel
    {
        public override bool CanEnchant(CardModel card) => false;
    }

    public sealed class PermissiveEnchantment : EnchantmentModel
    {
        public override bool CanEnchant(CardModel card) => true;
    }

    public sealed class StackableEnchantment : EnchantmentModel
    {
        public override bool IsStackable => true;
    }

    public sealed class GlamStyleEnchantment : EnchantmentModel
    {
        public override int EnchantPlayCount(int originalPlayCount) =>
            Status == EnchantmentStatus.Normal
                ? originalPlayCount + 1
                : originalPlayCount;

        public override Task AfterCardPlayed(CardPlay cardPlay)
        {
            if (Status == EnchantmentStatus.Normal && ReferenceEquals(cardPlay.Card, Owner))
            {
                Status = EnchantmentStatus.Disabled;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CountingPlayCard : CardModel
    {
        public int TimesPlayed { get; private set; }

        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Common;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 0;

        protected override Task OnPlay(CardPlay cardPlay)
        {
            TimesPlayed++;
            return Task.CompletedTask;
        }
    }

    private sealed class TypedCard(CardType type) : CardModel
    {
        public override CardType Type => type;

        public override CardRarity Rarity => CardRarity.Common;

        public override TargetType TargetType => TargetType.None;

        protected override int CanonicalEnergyCost => 0;
    }

    private sealed class DoublingModifierCard : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Common;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 0;

        public override int ModifyCardPlayCount(CardModel card, Sts2Sim.Core.Entities.Creatures.Creature? target, int playCount) =>
            playCount * 2;
    }
}
