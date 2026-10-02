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
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Potions;

file sealed class Task9CostlyCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 2;
}

file sealed class Task9ZeroCostCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
}

file sealed class Task9StarCostCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override int CanonicalStarCost => 1;
}
file sealed class Task9XStarCostCard : CardModel
{
    public override CardType Type => CardType.Skill;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.Self;
    protected override int CanonicalEnergyCost => 0;
    protected override bool IsXStarCost => true;
}

[Collection("ModelDb")]
public sealed class UncommonPotionBatch3Tests : IDisposable
{
    public UncommonPotionBatch3Tests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(
            ContentRegistry.AllTypes.Concat(new[]
            {
                typeof(UncommonPotionBatch3TestCharacter),
                typeof(Task9CostlyCard),
                typeof(Task9ZeroCostCard),
                typeof(Task9StarCostCard),
                typeof(Task9XStarCostCard),
            }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    public static TheoryData<string> MetadataCases => new()
    {
        "RegenPotion",
        "StableSerum",
        "TouchOfInsanity",
    };

    [Theory]
    [MemberData(nameof(MetadataCases))]
    public void Metadata_IsExactlyUncommonCombatOnlyAnyPlayer(string potionName)
    {
        PotionModel potion = GetCanonicalPotion(potionName);

        Assert.Equal(PotionRarity.Uncommon, potion.Rarity);
        Assert.Equal(PotionUsage.CombatOnly, potion.Usage);
        Assert.Equal(TargetType.AnyPlayer, potion.TargetType);
    }

    [Fact]
    public void RegenPotion_CannotBeGeneratedInCombat()
    {
        // 合并前审查发现真实源码里 RegenPotion 也有 `CanBeGeneratedInCombat => false`（计划文档的
        // Uncommon 参考表当时只标注了 FairyInABottle/FruitJuice 两个，漏了这一个）——锁定回归。
        PotionModel potion = GetCanonicalPotion("RegenPotion");

        Assert.False(potion.CanBeGeneratedInCombat);
    }

    [Fact]
    public async Task RegenPotion_HealsDescendingAmountsOnlyAtOwnerSideEarlyEndThenRemoves()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("regen-descending");
        player.Creature.LoseHpInternal(30m, ValueProp.Unpowered);

        await UsePotionAsync("RegenPotion", player, player.Creature);

        PowerModel regen = Assert.Single(
            player.Creature.Powers,
            power => power.GetType() == RequireTask9Type("RegenPower"));
        Assert.Equal(PowerType.Buff, regen.Type);
        Assert.Equal(PowerStackType.Counter, regen.StackType);
        Assert.Equal(5, regen.Amount);
        Assert.Same(player.Creature, regen.Applier);
        Assert.DoesNotContain(player.PotionSlots, potion => potion is not null);
        Assert.Equal(45, player.Creature.CurrentHp);

        await Hook.BeforeSideTurnEndEarly(
            room.Engine.State,
            CombatSide.Enemy,
            room.Engine.State.Enemies);
        Assert.Equal(45, player.Creature.CurrentHp);
        Assert.Equal(5, regen.Amount);

        int[] expectedHp = [50, 54, 57, 59, 60];
        for (int index = 0; index < expectedHp.Length; index++)
        {
            await Hook.BeforeSideTurnEndEarly(
                room.Engine.State,
                CombatSide.Player,
                room.Engine.State.Allies);

            int expectedAmount = 4 - index;
            Assert.Equal(expectedHp[index], player.Creature.CurrentHp);
            Assert.Equal(expectedAmount, regen.Amount);
            Assert.Equal(expectedAmount > 0, player.Creature.Powers.Contains(regen));
        }

        await Hook.BeforeSideTurnEndEarly(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        Assert.Equal(60, player.Creature.CurrentHp);

        (Player donor, CombatRoom sourceRoom) =
            await CreateCombatAsync("regen-owner-source", playerCount: 2);
        Creature recipient = sourceRoom.Engine.State.Allies.Single(creature => creature != donor.Creature);
        await UsePotionAsync("RegenPotion", donor, recipient);
        PowerModel recipientRegen = Assert.Single(
            recipient.Powers,
            power => power.GetType() == RequireTask9Type("RegenPower"));
        Assert.Equal(5, recipientRegen.Amount);
        Assert.Same(donor.Creature, recipientRegen.Applier);
        Assert.DoesNotContain(donor.PotionSlots, potion => potion is not null);
    }

    [Fact]
    public async Task RegenPower_DoesNotHealOrDecayADeadOwner()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("regen-dead");
        await UsePotionAsync("RegenPotion", player, player.Creature);
        PowerModel regen = Assert.Single(
            player.Creature.Powers,
            power => power.GetType() == RequireTask9Type("RegenPower"));
        player.Creature.LoseHpInternal(player.Creature.CurrentHp, ValueProp.Unpowered);

        await Hook.BeforeSideTurnEndEarly(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);

        Assert.Equal(0, player.Creature.CurrentHp);
        Assert.Equal(5, regen.Amount);
        Assert.Contains(regen, player.Creature.Powers);
    }

    [Fact]
    public async Task StableSerum_AppliesTwoRetainedTurnsThenNormalDiscardResumes()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("stable-serum");
        ClearHand(player);
        Task9CostlyCard retained = AddToHand<Task9CostlyCard>(player);

        await UsePotionAsync("StableSerum", player, player.Creature);

        RetainHandPower retain = Assert.Single(player.Creature.Powers.OfType<RetainHandPower>());
        Assert.Equal(2, retain.Amount);
        Assert.Same(player.Creature, retain.Applier);
        Assert.DoesNotContain(player.PotionSlots, potion => potion is not null);

        await room.Engine.EndPlayerTurnAsync();
        Assert.Contains(retained, player.PlayerCombatState!.Hand.Cards);
        Assert.Equal(1, retain.Amount);

        await room.Engine.EndPlayerTurnAsync();
        Assert.Contains(retained, player.PlayerCombatState.Hand.Cards);
        Assert.DoesNotContain(retain, player.Creature.Powers);

        for (int index = 0; index < 5; index++)
        {
            Task9ZeroCostCard filler = (Task9ZeroCostCard)ModelDb.Card<Task9ZeroCostCard>().MutableClone();
            filler.AssignOwner(player);
            CardPileCmd.Add(filler, PileType.Draw);
        }

        await room.Engine.EndPlayerTurnAsync();
        Assert.DoesNotContain(retained, player.PlayerCombatState.Hand.Cards);
        Assert.Contains(retained, player.PlayerCombatState.DiscardPile.Cards);

        (Player donor, CombatRoom sourceRoom) =
            await CreateCombatAsync("stable-serum-owner-source", playerCount: 2);
        Creature recipient = sourceRoom.Engine.State.Allies.Single(creature => creature != donor.Creature);
        await UsePotionAsync("StableSerum", donor, recipient);
        RetainHandPower recipientRetain = Assert.Single(recipient.Powers.OfType<RetainHandPower>());
        Assert.Equal(2, recipientRetain.Amount);
        Assert.Same(donor.Creature, recipientRetain.Applier);
        Assert.DoesNotContain(donor.PotionSlots, potion => potion is not null);
    }

    [Fact]
    public async Task TouchOfInsanity_SelectsFirstStillCostlyCardAndFreeStateSurvivesTheWholeCombat()
    {
        (Player player, _) = await CreateCombatAsync("touch-cost-priority");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearHand(player);
        Task9ZeroCostCard noCost = AddToHand<Task9ZeroCostCard>(player);
        Task9CostlyCard alreadyFree = AddToHand<Task9CostlyCard>(player);
        alreadyFree.MakeFreeUntilPlayed();
        Task9CostlyCard firstEligible = AddToHand<Task9CostlyCard>(player);
        Task9CostlyCard secondEligible = AddToHand<Task9CostlyCard>(player);

        await UsePotionAsync("TouchOfInsanity", player, player.Creature);

        // 偏离 #315：权威用 SetToFreeThisCombat——整场战斗免费，打出后不清除。
        Assert.False(noCost.TemporaryFreeThisCombat);
        Assert.True(alreadyFree.TemporaryFreeUntilPlayed);   // 药水应跳过它（已是免费态）
        Assert.False(alreadyFree.TemporaryFreeThisCombat);
        Assert.True(firstEligible.TemporaryFreeThisCombat);
        Assert.False(secondEligible.TemporaryFreeThisCombat);
        Assert.Equal(0, firstEligible.EnergyCost);

        player.PlayerCombatState!.EndOfTurnCleanup();
        Assert.True(firstEligible.TemporaryFreeThisCombat);
        Assert.Equal(0, firstEligible.EnergyCost);

        int energyBefore = player.PlayerCombatState.Energy;

        await firstEligible.PlayAsync(target: null);

        Assert.Equal(energyBefore, player.PlayerCombatState.Energy);
        // 关键差异：打出一次之后仍然免费。
        Assert.True(firstEligible.TemporaryFreeThisCombat);
        Assert.Equal(0, firstEligible.EnergyCost);

        // Pounce's FreeSkillPower changes the displayed cost through a global late hook.
        // The native potion still sees LegSweep's local cost of 2.
        ClearHand(player);
        LegSweep legSweep = AddToHand<LegSweep>(player);
        Task9ZeroCostCard nextSkill = AddToHand<Task9ZeroCostCard>(player);
        await PowerCmd.Apply<FreeSkillPower>(player.Creature.CombatState!, player.Creature, 1m, player.Creature, null);
        Assert.Equal(0, legSweep.EnergyCost);

        await UsePotionAsync("TouchOfInsanity", player, player.Creature);

        Assert.True(legSweep.TemporaryFreeThisCombat);
        await nextSkill.PlayAsync(target: null);
        Assert.DoesNotContain(player.Creature.Powers, power => power is FreeSkillPower);
        CardPileCmd.Add(legSweep, PileType.Draw);
        CardPileCmd.Add(legSweep, PileType.Hand);
        Assert.Equal(0, legSweep.EnergyCost);
    }

    [Fact]
    public async Task TouchOfInsanity_RecognizesAStarCostAsAValidCandidate()
    {
        (Player player, _) = await CreateCombatAsync("touch-star-cost");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearHand(player);
        AddToHand<Task9ZeroCostCard>(player);
        Task9StarCostCard starCost = AddToHand<Task9StarCostCard>(player);
        await PlayerCmd.GainStars(2, player);
        int starsBefore = player.PlayerCombatState!.Stars;

        await UsePotionAsync("TouchOfInsanity", player, player.Creature);

        Assert.True(starCost.TemporaryFreeThisCombat);
        Assert.Equal(0, starCost.StarCost);

        await starCost.PlayAsync(target: null);

        Assert.Equal(starsBefore, player.PlayerCombatState.Stars);
        // 偏离 #315：整场免费，打出后不清除。
        Assert.True(starCost.TemporaryFreeThisCombat);
        Assert.Equal(0, starCost.StarCost);
    }

    [Fact]
    public async Task TouchOfInsanity_SkipsAStarCardAlreadyFreeThisTurn()
    {
        (Player player, _) = await CreateCombatAsync("touch-skip-temporary-free-star");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearHand(player);
        Task9StarCostCard alreadyFree = AddToHand<Task9StarCostCard>(player);
        alreadyFree.MakeTemporaryFreeThisTurn();
        Task9CostlyCard stillCostly = AddToHand<Task9CostlyCard>(player);

        await UsePotionAsync("TouchOfInsanity", player, player.Creature);

        Assert.False(alreadyFree.TemporaryFreeThisCombat);
        Assert.True(stillCostly.TemporaryFreeThisCombat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TouchOfInsanity_SkipsPureXCosts_AndXStarsStillSpendOnPlay(bool xStars)
    {
        (Player player, _) = await CreateCombatAsync($"touch-x-cost-{xStars}");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearHand(player);
        CardModel xCard = xStars
            ? AddToHand<Task9XStarCostCard>(player)
            : AddToHand<Whirlwind>(player);
        await PlayerCmd.GainStars(3, player);
        int starsBefore = player.PlayerCombatState!.Stars;

        await UsePotionAsync("TouchOfInsanity", player, player.Creature);

        Assert.False(xCard.TemporaryFreeThisCombat);
        if (xStars)
        {
            Assert.Equal(starsBefore, xCard.StarCost);
            await xCard.PlayAsync(target: null);
            Assert.Equal(0, player.PlayerCombatState.Stars);
        }
    }

    [Fact]
    public async Task TouchOfInsanity_WithNoStillCostlyCandidateSkipsCleanly()
    {
        (Player player, _) = await CreateCombatAsync("touch-no-candidate");
        var selection = new LegacySelectionDecisionSource();
        ((Sts2Sim.Core.Combat.CombatState)player.Creature.CombatState!).CardSelectionSource = selection;
        ClearHand(player);
        Task9ZeroCostCard noCost = AddToHand<Task9ZeroCostCard>(player);
        Task9CostlyCard alreadyFree = AddToHand<Task9CostlyCard>(player);
        alreadyFree.MakeFreeUntilPlayed();

        await UsePotionAsync("TouchOfInsanity", player, player.Creature);

        Assert.False(noCost.TemporaryFreeUntilPlayed);
        Assert.True(alreadyFree.TemporaryFreeUntilPlayed);
        Assert.DoesNotContain(player.PotionSlots, potion => potion is not null);
    }

    private static PotionModel GetCanonicalPotion(string potionName)
    {
        Type potionType = RequireTask9Type(potionName);
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

    private static Type RequireTask9Type(string name)
    {
        string category = name.EndsWith("Power", StringComparison.Ordinal)
            ? "Powers"
            : "Potions";
        return typeof(PotionModel).Assembly.GetType($"Sts2Sim.Core.Models.{category}.{name}")
            ?? throw new Xunit.Sdk.XunitException($"Task 9 model {name} is not implemented.");
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static void ClearHand(Player player)
    {
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards.ToList())
        {
            CardPileCmd.Add(card, PileType.Discard);
        }
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(
        string seed,
        int playerCount = 1)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(
            ModelDb.Character<UncommonPotionBatch3TestCharacter>(),
            runState);
        runState.AddPlayer(player);
        for (int index = 1; index < playerCount; index++)
        {
            runState.AddPlayer(Player.CreateForNewRun(
                ModelDb.Character<UncommonPotionBatch3TestCharacter>(),
                runState));
        }
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}

file sealed class UncommonPotionBatch3TestCharacter : CharacterModel
{
    public override int StartingHp => 75;

    public override int StartingGold => 99;
}
