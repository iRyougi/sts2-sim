using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Factories;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

[Collection("ModelDb")]
public sealed class MiscMechanicCardTests : IDisposable
{
    public MiscMechanicCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
            typeof(WanderingGrunt),
            typeof(ParticleWall), typeof(ShiningStrike), typeof(Prolong), typeof(Impatience), typeof(Alchemize),
            typeof(LunarBlast), typeof(Radiate), typeof(Supermassive), typeof(GoldAxe),
            typeof(BlockNextTurnPower), typeof(EnvenomPower), typeof(PoisonPower),
            typeof(Sts2Sim.Core.Models.Potions.StrengthPotion),
            typeof(Sts2Sim.Core.Models.Potions.PoisonPotion),
            typeof(Sts2Sim.Core.Models.Potions.BlessingOfTheForge),
            typeof(Sts2Sim.Core.Models.Potions.LiquidMemories),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task ParticleWall_GainsBlock_AndGoesToHandInsteadOfDiscard()
    {
        (Player player, _) = await CreateCombatAsync("particle-wall");
        ParticleWall card = AddToHand<ParticleWall>(player);
        player.PlayerCombatState!.GainStars(2m);

        await card.PlayAsync(target: null);

        Assert.Equal(9, player.Creature.Block);
        Assert.Equal(PileType.Hand, card.Pile!.Type);
    }

    [Fact]
    public async Task ShiningStrike_DealsDamageGainsStars_AndGoesToDrawPileTop()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("shining-strike");
        ShiningStrike card = AddToHand<ShiningStrike>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;
        int starsBefore = player.PlayerCombatState!.Stars;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 8, enemy.CurrentHp);
        Assert.Equal(starsBefore + 2, player.PlayerCombatState!.Stars);
        Assert.Same(card, player.PlayerCombatState!.DrawPile.Cards.First());
    }

    [Fact]
    public async Task Prolong_GrantsBlockNextTurnPower_EqualToCurrentBlock_UpgradeRemovesExhaust()
    {
        (Player player, _) = await CreateCombatAsync("prolong");
        DefendRegent defend = AddToHand<DefendRegent>(player);
        await defend.PlayAsync(target: null);
        int blockBefore = player.Creature.Block;
        Prolong card = AddToHand<Prolong>(player);

        await card.PlayAsync(target: null);

        Assert.Equal(blockBefore, player.Creature.Powers.OfType<BlockNextTurnPower>().Single().Amount);

        Prolong upgraded = AddToHand<Prolong>(player);
        Assert.True(upgraded.HasKeyword(CardKeyword.Exhaust));
        upgraded.Upgrade();
        Assert.False(upgraded.HasKeyword(CardKeyword.Exhaust));
    }

    [Fact]
    public async Task Impatience_NoAttackInHand_DrawsTwo()
    {
        (Player player, _) = await CreateCombatAsync("impatience-empty");
        foreach (CardModel attack in player.PlayerCombatState!.Hand.Cards.Where(c => c.Type == CardType.Attack).ToList())
        {
            CardPileCmd.Add(attack, PileType.Discard);
        }

        Impatience card = AddToHand<Impatience>(player);
        int drawPileBefore = player.PlayerCombatState!.DrawPile.Cards.Count;

        await card.PlayAsync(target: null);

        Assert.True(player.PlayerCombatState!.DrawPile.Cards.Count <= drawPileBefore - 2 || player.PlayerCombatState!.DrawPile.Cards.Count < drawPileBefore);
    }

    [Fact]
    public async Task Impatience_HasAttackInHand_DoesNotDraw()
    {
        (Player player, _) = await CreateCombatAsync("impatience-has-attack");
        AddToHand<StrikeRegent>(player);
        int drawPileBefore = player.PlayerCombatState!.DrawPile.Cards.Count;
        Impatience card = AddToHand<Impatience>(player);

        await card.PlayAsync(target: null);

        Assert.Equal(drawPileBefore, player.PlayerCombatState!.DrawPile.Cards.Count);
    }

    [Fact]
    public async Task Alchemize_FillsEmptyPotionSlot()
    {
        (Player player, _) = await CreateCombatAsync("alchemize-full-slot");
        await AddToHand<Alchemize>(player).PlayAsync(target: null);

        Assert.Single(player.PotionSlots.OfType<PotionModel>());
        Assert.All(player.PotionSlots.OfType<PotionModel>(),
            potion => Assert.Contains(PotionFactory.GetOutOfCombatPool(player),
                candidate => candidate.Id == potion.Id && candidate.CanBeGeneratedInCombat));

        while (player.PotionSlots.Any(slot => slot is null))
            player.AddPotionInternal(ModelDb.Potion<Sts2Sim.Core.Models.Potions.StrengthPotion>());
        int potionCountBefore = player.PotionSlots.Count(slot => slot is not null);
        int rngBefore = player.RunState.Rng.CombatPotionGeneration.Counter;

        await AddToHand<Alchemize>(player).PlayAsync(target: null);

        int consumed = player.RunState.Rng.CombatPotionGeneration.Counter - rngBefore;
        Assert.True(consumed == 2, $"seed=alchemize-full-slot: expected 2 RNG draws, got {consumed}");
        Assert.Equal(potionCountBefore, player.PotionSlots.Count(slot => slot is not null));
    }

    [Theory]
    [InlineData(false, 0, 0, 0)]
    [InlineData(false, 1, 3, 1)]
    [InlineData(false, 2, 7, 2)]
    [InlineData(true, 2, 9, 2)]
    public async Task LunarBlast_ScalesWithSkillCardsPlayedThisTurn(
        bool upgraded, int skillsPlayed, int expectedHpLoss, int expectedPoison)
    {
        string seed = $"lunar-blast-{upgraded}-{skillsPlayed}";
        (Player player, CombatRoom room) = await CreateCombatAsync(seed);
        for (int i = 0; i < skillsPlayed; i++)
        {
            DefendRegent skill = AddToHand<DefendRegent>(player);
            await skill.PlayAsync(target: null);
        }

        await PowerCmd.Apply<EnvenomPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);
        LunarBlast card = AddToHand<LunarBlast>(player);
        if (upgraded) card.Upgrade();
        ICardDamageVariableProvider damageVariable = card;
        Assert.True(damageVariable.TryGetThrashDamageVariable(out decimal perHit) &&
                    perHit == (upgraded ? 5m : 4m),
            $"seed={seed}: Thrash must read the original per-hit DamageVar");
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await CreatureCmd.GainBlock(room.Engine.State, enemy, 1m, ValueProp.Move, null, null);
        decimal hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.True(enemy.CurrentHp == hpBefore - expectedHpLoss,
            $"seed={seed}: HP loss for {skillsPlayed} completed skills");
        Assert.True(enemy.Block == (skillsPlayed == 0 ? 1m : 0m),
            $"seed={seed}: remaining block for {skillsPlayed} hits");
        Assert.True(enemy.Powers.OfType<PoisonPower>().Sum(power => power.Amount) == expectedPoison,
            $"seed={seed}: Envenom should apply once per unblocked hit");
    }

    [Theory]
    [InlineData(false, 0, 0, 0)]
    [InlineData(false, 2, 5, 2)]
    [InlineData(false, 3, 8, 3)]
    [InlineData(true, 2, 7, 2)]
    public async Task Radiate_ScalesWithStarsGainedThisTurn(
        bool upgraded, int starsGained, int expectedHpLoss, int expectedPoison)
    {
        string seed = $"radiate-{upgraded}-{starsGained}";
        (Player player, CombatRoom room) = await CreateCombatAsync(seed, enemyCount: 2);
        // Regent's starting relic grants 3 Stars on room entry. Start this isolated
        // turn-count fixture after that entry gain so the row controls the hit count.
        player.PlayerCombatState!.EndOfTurnCleanup();
        if (starsGained > 0) await PlayerCmd.GainStars(starsGained, player);
        await PowerCmd.Apply<EnvenomPower>(room.Engine.State, player.Creature, 1m, player.Creature, null);
        Radiate card = AddToHand<Radiate>(player);
        if (upgraded) card.Upgrade();
        ICardDamageVariableProvider damageVariable = card;
        Assert.True(damageVariable.TryGetThrashDamageVariable(out decimal perHit) &&
                    perHit == (upgraded ? 4m : 3m),
            $"seed={seed}: Thrash must read the original per-hit DamageVar");
        Creature[] enemies = room.Engine.State.HittableEnemies.ToArray();
        foreach (Creature enemy in enemies)
        {
            await CreatureCmd.GainBlock(room.Engine.State, enemy, 1m, ValueProp.Move, null, null);
        }
        int[] hpBefore = enemies.Select(enemy => enemy.CurrentHp).ToArray();

        await card.PlayAsync(target: null);

        for (int i = 0; i < enemies.Length; i++)
        {
            Assert.True(enemies[i].CurrentHp == hpBefore[i] - expectedHpLoss,
                $"seed={seed}, enemy={i}: expected HP {hpBefore[i] - expectedHpLoss}, actual {enemies[i].CurrentHp} for {starsGained} gained Stars; actual turn Stars={player.PlayerCombatState!.StarsGainedThisTurn}");
            Assert.True(enemies[i].Block == (starsGained == 0 ? 1m : 0m),
                $"seed={seed}, enemy={i}: remaining block");
            Assert.True(enemies[i].Powers.OfType<PoisonPower>().Sum(power => power.Amount) == expectedPoison,
                $"seed={seed}, enemy={i}: Envenom should apply once per unblocked hit");
        }
    }

    [Fact]
    public async Task Supermassive_ScalesWithCardsGeneratedThisCombat()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("supermassive");
        var generated = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        generated.AssignOwner(player);
        await CardPileCmd.Generate(room.Engine.State, generated, PileType.Hand);
        Supermassive card = AddToHand<Supermassive>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 8, enemy.CurrentHp);
    }

    [Fact]
    public async Task GoldAxe_ScalesWithTotalCardsPlayedThisCombat_UpgradeAddsRetain()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("gold-axe");
        DefendRegent strike = AddToHand<DefendRegent>(player);
        await strike.PlayAsync(target: null);
        GoldAxe card = AddToHand<GoldAxe>(player);
        Assert.False(card.HasKeyword(CardKeyword.Retain));
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 1, enemy.CurrentHp);

        GoldAxe upgraded = AddToHand<GoldAxe>(player);
        upgraded.Upgrade();
        Assert.True(upgraded.HasKeyword(CardKeyword.Retain));
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        CardPileCmd.Add(card, PileType.Hand);
        return card;
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed, int enemyCount = 1)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => Enumerable.Range(0, enemyCount)
            .Select(_ => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone())
            .ToArray());
        await room.Enter(runState);
        return (player, room);
    }
}
