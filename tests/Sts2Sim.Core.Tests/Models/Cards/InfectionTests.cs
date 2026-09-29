using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
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
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Models.Cards;

file sealed class InfectionDamageSourceProbe : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public Creature? ObservedDealer { get; private set; }

    public CardModel? ObservedCardSource { get; private set; }

    public override Task BeforeDamageReceived(
        Creature target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        ObservedDealer = dealer;
        ObservedCardSource = cardSource;
        return Task.CompletedTask;
    }
}

[Collection("ModelDb")]
public sealed class InfectionTests : IDisposable
{
    public InfectionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Infection),
            typeof(InfectionDamageSourceProbe),
            typeof(WanderingGrunt),
            typeof(Regent),
            typeof(Sts2Sim.Core.Models.Cards.StrikeRegent),
            typeof(Sts2Sim.Core.Models.Cards.DefendRegent),
            typeof(Sts2Sim.Core.Models.Cards.FallingStar),
            typeof(Sts2Sim.Core.Models.Cards.Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public void CanonicalValues_MatchSourceStatusCard()
    {
        Infection card = ModelDb.Card<Infection>();

        Assert.Equal(-1, card.EnergyCost);
        Assert.Equal(CardType.Status, card.Type);
        Assert.Equal(CardRarity.Status, card.Rarity);
        Assert.Equal(TargetType.None, card.TargetType);
        Assert.Equal(0, card.MaxUpgradeLevel);
        Assert.Contains(CardKeyword.Unplayable, card.Keywords);
    }

    [Fact]
    public async Task BeforePlayerSideTurnEnd_WhileInHand_DealsThreeUnpoweredMoveDamageThroughNormalBlock()
    {
        (RunState runState, Player player, CombatRoom room) = await EnterCombat();
        var infection = (Infection)ModelDb.Card<Infection>().MutableClone();
        infection.AssignOwner(player);
        await CardPileCmd.Generate(room.Engine.State, infection, PileType.Hand);
        player.Creature.GainBlockInternal(2m);
        int hpBefore = player.Creature.CurrentHp;

        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        await CombatEngine.DoTurnEndAsync(room.Engine.State, player);

        Assert.Equal(hpBefore - 1, player.Creature.CurrentHp);
        Assert.Equal(0, player.Creature.Block);
        // 原版 DoTurnEndCards：移入打出区结算后放到弃牌堆底。
        Assert.Same(player.PlayerCombatState!.DiscardPile, infection.Pile);
        await room.Exit(runState);
    }

    [Fact]
    public async Task EndPlayerTurnAsync_TriggersInHandDamageBeforeDiscardingInfection()
    {
        (RunState runState, Player player, CombatRoom room) = await EnterCombat();
        var infection = (Infection)ModelDb.Card<Infection>().MutableClone();
        infection.AssignOwner(player);
        await CardPileCmd.Generate(room.Engine.State, infection, PileType.Hand);
        player.Creature.GainBlockInternal(2m);
        int hpBefore = player.Creature.CurrentHp;

        await room.Engine.EndPlayerTurnAsync();

        Assert.Equal(hpBefore - 8, player.Creature.CurrentHp); // 1 from Infection after block, then WanderingGrunt's 7.
        Assert.Equal(0, player.Creature.Block);
        Assert.Contains(infection, player.PlayerCombatState!.DiscardPile.Cards);
        await room.Exit(runState);
    }

    [Fact]
    public async Task EndPlayerTurnAsync_WhenInfectionKillsLastPlayer_StopsBeforeDiscardAndEnemySide()
    {
        (RunState runState, Player player, CombatRoom room) = await EnterCombat();
        var infection = (Infection)ModelDb.Card<Infection>().MutableClone();
        infection.AssignOwner(player);
        await CardPileCmd.Generate(room.Engine.State, infection, PileType.Hand);
        player.Creature.LoseHpInternal(player.Creature.CurrentHp - 3m, default);

        await room.Engine.EndPlayerTurnAsync();

        Assert.False(room.Engine.IsInProgress);
        Assert.False(room.Engine.Won);
        Assert.Equal(CombatSide.Player, room.Engine.State.CurrentSide);
        // 回合末处理之后的胜负检查拦在阶段二之前，其余手牌不再弃。
        // 不断言 Infection 的去向：原版 CardPileCmd.Add 在 IsEnding 或持有者已死时不移牌，Infection 留在打出区；
        // 模拟器的 CardPileCmd.Add 还没有这道守卫（#83 计划 §5），会把它放进弃牌堆。
        Assert.NotEmpty(player.PlayerCombatState!.Hand.Cards);
        await room.Exit(runState);
    }

    [Fact]
    public async Task InHandDamage_ReportsOwnerAsDealerAndInfectionAsCardSource()
    {
        (RunState runState, Player player, CombatRoom room) = await EnterCombat();
        var probe = (InfectionDamageSourceProbe)ModelDb.Relic<InfectionDamageSourceProbe>().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        var infection = (Infection)ModelDb.Card<Infection>().MutableClone();
        infection.AssignOwner(player);
        await CardPileCmd.Generate(room.Engine.State, infection, PileType.Hand);

        await Hook.BeforeSideTurnEnd(
            room.Engine.State,
            CombatSide.Player,
            room.Engine.State.Allies);
        await CombatEngine.DoTurnEndAsync(room.Engine.State, player);

        Assert.Same(player.Creature, probe.ObservedDealer);
        Assert.Same(infection, probe.ObservedCardSource);
        await room.Exit(runState);
    }

    private static async Task<(RunState RunState, Player Player, CombatRoom Room)> EnterCombat()
    {
        var runState = new RunState("infection-turn-end", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        return (runState, player, room);
    }
}
