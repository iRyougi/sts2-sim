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

/// <summary>
/// 抽测边界：手牌里只有这一张牌，而它自带"丢弃其他牌"的效果时会发生什么。
/// <para>
/// 两个风险点：一是打出的牌若在 <c>OnPlay</c> 时仍留在手牌，会把自己列为丢弃候选，
/// 变成自我丢弃；二是无牌可丢时若去询问决策源，搜索器会卡在一个没有合法选项的决策点上。
/// </para>
/// </summary>
[Collection("ModelDb")]
public sealed class DiscardWithEmptyHandTests : IDisposable
{
    public DiscardWithEmptyHandTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(Sts2Sim.Core.Content.ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    /// <summary>Survivor：加格挡后丢 1 张。手牌只有它自己时，打出后手牌为空。</summary>
    [Fact]
    public async Task Survivor_AsOnlyCardInHand_DoesNotDiscardItselfAndStillGainsBlock()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("discard-empty-survivor");
        ClearAllPiles(player);
        var survivor = (Survivor)ModelDb.Card<Survivor>().MutableClone();
        survivor.AssignOwner(player);
        CardPileCmd.Add(survivor, PileType.Hand);

        Assert.Single(player.PlayerCombatState!.Hand.Cards);
        decimal blockBefore = player.Creature.Block;

        await survivor.PlayAsync(null);

        // 没有抛异常，格挡照常拿到。
        Assert.Equal(blockBefore + 8m, player.Creature.Block);
        // 手牌空了，但它是被"打出"消耗的，不是被自己丢弃的。
        Assert.Empty(player.PlayerCombatState!.Hand.Cards);
        Assert.DoesNotContain(survivor, player.PlayerCombatState!.Hand.Cards);
    }

    /// <summary>Prepared：先抽 1 再丢 1，且没有 Survivor 那样的空集合保护。
    /// 抽牌堆与弃牌堆都空时，抽不到牌，随后要丢的手牌也是空的。</summary>
    [Fact]
    public async Task Prepared_AsOnlyCardWithEmptyDrawPile_CompletesWithoutThrowing()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("discard-empty-prepared");
        ClearAllPiles(player);
        var prepared = (Prepared)ModelDb.Card<Prepared>().MutableClone();
        prepared.AssignOwner(player);
        CardPileCmd.Add(prepared, PileType.Hand);

        Assert.Empty(player.PlayerCombatState!.DrawPile.Cards);
        Assert.Empty(player.PlayerCombatState!.DiscardPile.Cards);

        await prepared.PlayAsync(null);

        Assert.Empty(player.PlayerCombatState!.Hand.Cards);
    }

    /// <summary>手牌恰好只剩 1 张可丢时，不应该再去问决策源——候选数等于要求数，只有唯一解。</summary>
    [Fact]
    public async Task Survivor_WithExactlyOneOtherCard_AutoDiscardsItWithoutAsking()
    {
        (Player player, CombatRoom room) = await CreateCombatAsync("discard-exact-one");
        ClearAllPiles(player);
        var survivor = (Survivor)ModelDb.Card<Survivor>().MutableClone();
        survivor.AssignOwner(player);
        CardPileCmd.Add(survivor, PileType.Hand);
        var victim = (StrikeSilent)ModelDb.Card<StrikeSilent>().MutableClone();
        victim.AssignOwner(player);
        CardPileCmd.Add(victim, PileType.Hand);

        await survivor.PlayAsync(null);

        Assert.Empty(player.PlayerCombatState!.Hand.Cards);
        Assert.Contains(victim, player.PlayerCombatState!.DiscardPile.Cards);
    }

    private static void ClearAllPiles(Player player)
    {
        PlayerCombatState state = player.PlayerCombatState!;
        foreach (CardModel card in state.Hand.Cards.ToArray()
            .Concat(state.DrawPile.Cards.ToArray())
            .Concat(state.DiscardPile.Cards.ToArray()))
        {
            CardPileCmd.Add(card, PileType.Exhaust);
        }
    }

    private static async Task<(Player player, CombatRoom room)> CreateCombatAsync(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(runState);
        return (player, room);
    }
}
