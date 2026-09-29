using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Hooks;

[Collection("ModelDb")]
public sealed class StatefulModifierBoundaryTests : IDisposable
{
    public StatefulModifierBoundaryTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Append(typeof(BufferAmountProbe)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Buffer_FractionalUnblockedHitPreservesChargeForNextIntegerHit()
    {
        (Player player, CombatRoom room) = await CreateCombat();
        Creature enemy = room.Engine.State.Enemies.Single();
        await PowerCmd.Apply<BufferPower>(room.Engine.State, player.Creature, 1m, null, null);
        await PowerCmd.Apply<WeakPower>(room.Engine.State, enemy, 1m, null, null);
        player.Creature.GainBlockInternal(5m);
        int hp = player.Creature.CurrentHp;

        await CreatureCmd.Damage(room.Engine.State, [player.Creature], 7m,
            ValueProp.Move, enemy, null, null);

        Assert.Equal(hp, player.Creature.CurrentHp);
        Assert.Equal(1, player.Creature.GetPower<BufferPower>()?.Amount);

        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(0m, Hook.ModifyHpLost(room.Engine.State, player.Creature,
                2m, ValueProp.Unpowered, null, null, out _));
        }
        Assert.Equal(1, player.Creature.GetPower<BufferPower>()?.Amount);
        CombatState clone = room.Engine.State.Clone();
        await CreatureCmd.Damage(clone, [clone.Players[0].Creature], 2m,
            ValueProp.Unpowered, null, null, null);
        Assert.Null(clone.Players[0].Creature.GetPower<BufferPower>());
        Assert.Equal(1, player.Creature.GetPower<BufferPower>()?.Amount);
        var probe = (BufferAmountProbe)ModelDb.Power<BufferAmountProbe>().MutableClone();
        probe.ApplyInternal(player.Creature, 1m);
        Task damage = CreatureCmd.Damage(room.Engine.State, [player.Creature], 2m,
                ValueProp.Unpowered, null, null, null);

        Assert.True(probe.Entered);
        Assert.False(damage.IsCompleted);
        probe.Release.TrySetResult();
        await damage;
        Assert.True(probe.Completed);
        Assert.Null(player.Creature.GetPower<BufferPower>());
        Assert.Equal(hp, player.Creature.CurrentHp);
    }

    [Fact]
    public async Task SturdyClamp_QueryIsPureAndActualTurnCapsBlock()
    {
        (Player player, CombatRoom room) = await CreateCombat();
        await RelicCmd.Obtain(ModelDb.Relic<SturdyClamp>(), player);
        player.Creature.GainBlockInternal(20m);
        Assert.False(Hook.ShouldClearBlock(room.Engine.State, player.Creature));
        Assert.Equal(20, player.Creature.Block);

        await room.Engine.StartTurnAsync();
        Assert.Equal(10, player.Creature.Block);
        player.Creature.LoseBlockInternal(player.Creature.Block);
        await PowerCmd.Apply<BlurPower>(room.Engine.State, player.Creature, 1m, null, null);
        player.Creature.GainBlockInternal(20m);
        CombatState clone = room.Engine.State.Clone();

        await clone.Engine!.StartTurnAsync();
        Assert.Equal(20, clone.Players[0].Creature.Block);
        Assert.Equal(20, player.Creature.Block);
        await room.Engine.StartTurnAsync();
        Assert.Equal(20, player.Creature.Block);
    }

    [Fact]
    public async Task PollinousCore_ActiveQueriesArePureOnOriginalAndClone()
    {
        (Player player, CombatRoom room) = await CreateCombat();
        decimal baseDraw = Hook.ModifyHandDraw(room.Engine.State, player, 5m);
        await RelicCmd.Obtain(ModelDb.Relic<PollinousCore>(), player);
        for (int i = 0; i < 4; i++) await Hook.BeforeHandDraw(room.Engine.State, player);
        CombatState clone = room.Engine.State.Clone();

        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(baseDraw + 2m, Hook.ModifyHandDraw(clone, clone.Players[0], 5m));
            Assert.Equal(baseDraw + 2m, Hook.ModifyHandDraw(room.Engine.State, player, 5m));
        }
        decimal draw = Hook.ModifyHandDraw(room.Engine.State, player, 5m, out var modifiers);
        await Hook.AfterModifyingHandDraw(room.Engine.State, modifiers);
        Assert.Equal(baseDraw + 2m, draw);
        Assert.Equal(baseDraw, Hook.ModifyHandDraw(room.Engine.State, player, 5m));
        Assert.Equal(baseDraw + 2m, Hook.ModifyHandDraw(clone, clone.Players[0], 5m));
    }

    private static async Task<(Player Player, CombatRoom Room)> CreateCombat()
    {
        var run = new RunState("stateful-modifier-boundaries", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var room = new CombatRoom(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());
        await room.Enter(run);
        return (player, room);
    }

    private sealed class BufferAmountProbe : PowerModel
    {
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Entered { get; private set; }
        public bool Completed { get; private set; }

        public override async Task AfterPowerAmountChanged(PowerModel power, decimal amount,
            Creature? applier, CardModel? cardSource)
        {
            if (power is not BufferPower || amount >= 0m) return;
            Entered = true;
            await Release.Task;
            Completed = true;
        }
    }
}
