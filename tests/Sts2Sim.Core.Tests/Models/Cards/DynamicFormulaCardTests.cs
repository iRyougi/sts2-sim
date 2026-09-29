using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models.Cards;

file sealed class FormulaDebuffA : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

file sealed class FormulaDebuffB : PowerModel
{
    public override PowerType Type => PowerType.Debuff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

[Collection("ModelDb")]
public sealed class DynamicFormulaCardTests : IDisposable
{
    public DynamicFormulaCardTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent), typeof(StrikeRegent), typeof(DefendRegent), typeof(FallingStar), typeof(Venerate),
            typeof(CrescentSpear), typeof(Stardust), typeof(MindBlast), typeof(Rend),
            typeof(WeakPower), typeof(VulnerablePower), typeof(StrengthPower), typeof(VigorPower),
            typeof(CrushUnderPower), typeof(FormulaDebuffA), typeof(FormulaDebuffB),
            typeof(DivineRight), typeof(WanderingGrunt),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void HasStarCost_IncludesFixedAndXStarCards()
    {
        Assert.True(ModelDb.Card<CrescentSpear>().HasStarCost);
        Assert.True(ModelDb.Card<Stardust>().HasStarCost);
        Assert.False(ModelDb.Card<StrikeRegent>().HasStarCost);
    }

    [Fact]
    public async Task CrescentSpear_DamageScalesWithAllStarCostCards()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("crescent-formula");
        CrescentSpear card = AddToHand<CrescentSpear>(player);
        AddToPile<Stardust>(player, player.PlayerCombatState!.DiscardPile);
        player.PlayerCombatState.GainStars(10);
        int starCardCount = player.PlayerCombatState.AllPiles
            .SelectMany(pile => pile.Cards)
            .Count(candidate => candidate.HasStarCost);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - (8 + 2 * starCardCount), enemy.CurrentHp);
    }

    [Fact]
    public async Task CrescentSpear_Upgrade_IncreasesDamagePerStarCard()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("crescent-formula-upgrade");
        CrescentSpear card = AddToHand<CrescentSpear>(player);
        AddToPile<Stardust>(player, player.PlayerCombatState!.DiscardPile);
        card.Upgrade();
        player.PlayerCombatState.GainStars(10);
        int starCardCount = player.PlayerCombatState.AllPiles
            .SelectMany(pile => pile.Cards)
            .Count(candidate => candidate.HasStarCost);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - (8 + 3 * starCardCount), enemy.CurrentHp);
    }

    [Fact]
    public async Task MindBlast_DamageEqualsCurrentDrawPileSize()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("mind-blast-formula");
        MindBlast card = AddToHand<MindBlast>(player);
        int expectedDamage = player.PlayerCombatState!.DrawPile.Cards.Count;
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - expectedDamage, enemy.CurrentHp);
    }

    [Fact]
    public void MindBlast_Upgrade_ReducesEnergyCostToZero()
    {
        var card = (MindBlast)ModelDb.Card<MindBlast>().MutableClone();

        card.Upgrade();

        Assert.Equal(0, card.EnergyCost);
    }

    [Fact]
    public async Task Rend_CountsNonTemporaryDebuffs()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("rend-formula");
        Rend card = AddToHand<Rend>(player);
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<FormulaDebuffA>(room.Engine.State, enemy, 1m, player.Creature, null);
        await PowerCmd.Apply<FormulaDebuffB>(room.Engine.State, enemy, 1m, player.Creature, null);
        await PowerCmd.Apply<CrushUnderPower>(room.Engine.State, enemy, 1m, player.Creature, null);
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        // 正式版：10 + 5 × 3。CrushUnder 标记本身是临时能力不计数，但它施加的真实负力量按
        // TypeForCurrentAmount 算减益，与 FormulaDebuffA/B 一起共 3 个。
        Assert.Equal(hpBefore - 25, enemy.CurrentHp);
    }

    [Fact]
    public async Task Rend_Upgrade_IncreasesBaseAndPerDebuffDamage()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("rend-formula-upgrade");
        Rend card = AddToHand<Rend>(player);
        card.Upgrade();
        Creature enemy = room.Engine.State.HittableEnemies.Single();
        await PowerCmd.Apply<FormulaDebuffA>(room.Engine.State, enemy, 1m, player.Creature, null);
        await PowerCmd.Apply<FormulaDebuffB>(room.Engine.State, enemy, 1m, player.Creature, null);
        int hpBefore = enemy.CurrentHp;

        await card.PlayAsync(enemy);

        Assert.Equal(hpBefore - 28, enemy.CurrentHp);
    }

    private static TCard AddToHand<TCard>(Player player)
        where TCard : CardModel =>
        AddToPile<TCard>(player, player.PlayerCombatState!.Hand);

    private static TCard AddToPile<TCard>(Player player, CardPile pile)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        pile.AddInternal(card);
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
