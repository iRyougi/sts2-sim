using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
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

namespace Sts2Sim.Core.Tests.Combat;

file sealed class OstyHookProbe : RelicModel
{
    public override RelicRarity Rarity => RelicRarity.Common;

    public List<string> Calls { get; } = new();

    public decimal? SummonOverride { get; set; }

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        Calls.Add($"before:{OstyFixture.Name(target)}");
        return amount;
    }

    public override decimal ModifyHpLostAfterOsty(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        Calls.Add($"after:{OstyFixture.Name(target)}");
        return amount;
    }

    public override decimal ModifySummonAmount(Player summoner, decimal amount, AbstractModel? source) =>
        SummonOverride ?? amount;

    public override Task AfterSummon(Player summoner, decimal amount)
    {
        Calls.Add($"summon:{amount}");
        return Task.CompletedTask;
    }

    public override Task AfterOstyRevived(Creature osty)
    {
        Calls.Add("revived");
        return Task.CompletedTask;
    }

    public override Task BeforeDamageReceived(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        Calls.Add($"hit:{OstyFixture.Name(target)}<-{(dealer is null ? "none" : OstyFixture.Name(dealer))}" +
                  $":{cardSource?.GetType().Name ?? "none"}");
        return Task.CompletedTask;
    }
}

file static class OstyFixture
{
    public static string Name(Creature creature) =>
        creature.Monster is Osty ? "osty" : creature.IsPlayer ? "player" : "enemy";

    public static async Task<(Player Player, CombatRoom Room, OstyHookProbe Probe)> EnterCombat(string seed)
    {
        var runState = new RunState(seed, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        var room = new CombatRoom(() => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        runState.PushRoom(room);
        await room.Enter(runState);
        var probe = (OstyHookProbe)new OstyHookProbe().MutableClone();
        probe.AssignOwner(player);
        player.AddRelicInternal(probe);
        return (player, room, probe);
    }
}

// 原版 CreatureCmd.Damage：BeforeOsty → ModifyUnblockedDamageTarget（DieForYouPower）→ AfterOsty，溢出回到主人。
[Collection("ModelDb")]
public sealed class OstyDamageRedirectionTests : IDisposable
{
    public OstyDamageRedirectionTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("powered-redirects")]
    [InlineData("unpowered-hits-owner")]
    [InlineData("overflow-returns-to-owner")]
    [InlineData("dead-osty-does-not-redirect")]
    [InlineData("owner-block-first")]
    [InlineData("phase-order")]
    [InlineData("osty-attack-owner-relics")]
    [InlineData("osty-attack-personal-hive")]
    public async Task DamagePhasesAndRedirection_MatchSource(string scenario)
    {
        (Player player, CombatRoom room, OstyHookProbe probe) = await OstyFixture.EnterCombat($"osty-damage-{scenario}");
        CombatState state = room.Engine.State;
        Creature enemy = state.HittableEnemies.Single();
        await OstyCmd.Summon(player, scenario == "overflow-returns-to-owner" ? 3m : 10m, null);
        Creature osty = Assert.IsType<Creature>(player.Osty);
        if (scenario.StartsWith("osty-attack-", StringComparison.Ordinal))
        {
            await AssertOstyAttackCountsForOwner(scenario, player, state, enemy, osty);
            return;
        }
        if (scenario == "dead-osty-does-not-redirect")
            await CreatureCmd.Kill(osty);
        if (scenario == "owner-block-first")
            player.Creature.GainBlockInternal(4m);
        int playerHp = player.Creature.CurrentHp;
        probe.Calls.Clear();

        ValueProp props = scenario == "unpowered-hits-owner" ? ValueProp.Move | ValueProp.Unpowered : ValueProp.Move;
        IReadOnlyList<DamageResult> results = await CreatureCmd.Damage(state, [player.Creature], 8m, props, enemy, null, null);

        switch (scenario)
        {
            case "powered-redirects":
                Assert.Equal(new[] { osty, player.Creature }, results.Select(result => result.Receiver));
                Assert.Equal(2, osty.CurrentHp);
                Assert.Equal(playerHp, player.Creature.CurrentHp);
                break;
            case "unpowered-hits-owner":
                Assert.Equal(player.Creature, Assert.Single(results).Receiver);
                Assert.Equal(10, osty.CurrentHp);
                Assert.Equal(playerHp - 8, player.Creature.CurrentHp);
                break;
            case "overflow-returns-to-owner":
                Assert.True(osty.IsDead);
                Assert.Equal(playerHp - 5, player.Creature.CurrentHp);
                Assert.Equal(5, results[1].UnblockedDamage);
                // 死去的 Osty 留在战斗里，能力保留。
                Assert.Contains(osty, state.Allies);
                Assert.NotNull(osty.GetPower<DieForYouPower>());
                break;
            case "dead-osty-does-not-redirect":
                Assert.Equal(player.Creature, Assert.Single(results).Receiver);
                Assert.Equal(playerHp - 8, player.Creature.CurrentHp);
                break;
            case "owner-block-first":
                Assert.Equal(0, player.Creature.Block);
                Assert.Equal(6, osty.CurrentHp);
                Assert.Equal(4, results[1].BlockedDamage);
                Assert.Equal(0, results[0].BlockedDamage);
                // 破盾与完全格挡只记在原始目标那条上；Osty 承受了 4 点，所以不算完全格挡。
                Assert.True(results[1].WasBlockBroken);
                Assert.False(results[1].WasFullyBlocked);
                Assert.False(results[0].WasBlockBroken);
                // 直接命中 Osty 时由主人格挡吸收，主人还剩格挡，但原版看 Osty 自身格挡（恒为 0），算打破。
                player.Creature.GainBlockInternal(5m);
                DamageResult direct = Assert.Single(await CreatureCmd.Damage(state, [osty], 3m, ValueProp.Move, enemy, null, null));
                Assert.Equal(2, player.Creature.Block);
                Assert.True(direct.WasBlockBroken);
                Assert.True(direct.WasFullyBlocked);
                break;
            case "phase-order":
                Assert.Equal(new[] { "hit:player<-enemy:none", "before:player", "after:osty", "after:player" },
                    probe.Calls);
                break;
        }
    }

    // 原版把主人的 Osty 也算作主人一方的攻击者：TheBoot 抬到 5、HandDrill 认宠物破盾、PersonalHive 的 Dazed 给主人。
    private static async Task AssertOstyAttackCountsForOwner(
        string scenario, Player player, CombatState state, Creature enemy, Creature osty)
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);
        int enemyHp = enemy.CurrentHp;
        if (scenario == "osty-attack-owner-relics")
        {
            foreach (RelicModel relic in new RelicModel[] { ModelDb.Relic<TheBoot>(), ModelDb.Relic<HandDrill>() })
            {
                var owned = (RelicModel)relic.MutableClone();
                owned.AssignOwner(player);
                player.AddRelicInternal(owned);
            }

            enemy.GainBlockInternal(3m);
            await DamageCmd.Attack(4m).FromOsty(osty, card, null).Targeting(enemy).Execute();
            Assert.Equal(enemyHp - 5, enemy.CurrentHp);
            Assert.Equal(2, enemy.GetPower<VulnerablePower>()?.Amount);
            return;
        }

        await PowerCmd.Apply<PersonalHivePower>(state, enemy, 1m, enemy, null);
        int dazedBefore = player.PlayerCombatState!.DrawPile.Cards.OfType<Dazed>().Count();
        await DamageCmd.Attack(4m).FromOsty(osty, card, null).Targeting(enemy).Execute();
        Assert.Equal(dazedBefore + 1, player.PlayerCombatState.DrawPile.Cards.OfType<Dazed>().Count());
    }
}

[Collection("ModelDb")]
public sealed class OstySummonTests : IDisposable
{
    public OstySummonTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("first")]
    [InlineData("grow")]
    [InlineData("revive")]
    [InlineData("modified-to-zero")]
    public async Task SummonReviveAndGrow_MatchSource(string scenario)
    {
        (Player player, CombatRoom room, OstyHookProbe probe) = await OstyFixture.EnterCombat($"osty-summon-{scenario}");
        Assert.True(Osty.CheckMissingWithAnim(player));
        if (scenario == "modified-to-zero")
        {
            probe.SummonOverride = 0m;
            SummonResult none = await OstyCmd.Summon(player, 5m, null);
            Assert.Null(none.Osty);
            Assert.Equal(0m, none.Amount);
            Assert.Empty(probe.Calls);
            Assert.DoesNotContain(room.Engine.State.Allies, creature => creature.Monster is Osty);
            Assert.True(Osty.CheckMissingWithAnim(player));
            return;
        }

        SummonResult first = await OstyCmd.Summon(player, 5m, null);
        Creature osty = Assert.IsType<Creature>(first.Osty);
        Assert.Same(player, osty.PetOwner);
        Assert.NotNull(osty.GetPower<DieForYouPower>());
        Assert.Equal((5, 5), (osty.MaxHp, osty.CurrentHp));
        Assert.True(osty.Monster!.IsHealthBarVisible);
        Assert.False(Osty.CheckMissingWithAnim(player));

        switch (scenario)
        {
            case "first":
                Assert.True(player.IsOstyAlive);
                Assert.Equal(new[] { "summon:5" }, probe.Calls);
                Assert.True(ModelDb.Monster<WanderingGrunt>().IsHealthBarVisible);
                Assert.False(ModelDb.Monster<Sts2Sim.Core.Models.Monsters.Byrdpip>().IsHealthBarVisible);
                Assert.False(ModelDb.Monster<Sts2Sim.Core.Models.Monsters.PaelsLegion>().IsHealthBarVisible);
                Assert.Equal("Creature was accessed before it was set.",
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        _ = ModelDb.Monster<Osty>().IsHealthBarVisible;
                    }).Message);
                break;
            case "grow":
                await OstyCmd.Summon(player, 3m, null);
                Assert.Same(osty, player.Osty);
                Assert.Equal((8, 8), (osty.MaxHp, osty.CurrentHp));
                Assert.Equal(new[] { "summon:5", "summon:3" }, probe.Calls);
                break;
            case "revive":
                await CreatureCmd.Kill(osty);
                Assert.True(player.IsOstyMissing);
                Assert.False(osty.Monster!.IsHealthBarVisible);
                Assert.True(Osty.CheckMissingWithAnim(player));
                SummonResult revived = await OstyCmd.Summon(player, 4m, null);
                Assert.Same(osty, revived.Osty);
                Assert.Equal((4, 4), (osty.MaxHp, osty.CurrentHp));
                Assert.Single(osty.Powers.OfType<DieForYouPower>());
                Assert.Equal(new[] { "summon:5", "revived", "summon:4" }, probe.Calls);
                Assert.True(osty.Monster!.IsHealthBarVisible);
                Assert.False(Osty.CheckMissingWithAnim(player));
                break;
        }
    }
}

[Collection("ModelDb")]
public sealed class OstyLifecycleTests : IDisposable
{
    public OstyLifecycleTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("player-death-kills-osty")]
    [InlineData("from-osty-attack")]
    [InlineData("dead-osty-unhittable")]
    [InlineData("clone-isolation")]
    public async Task PlayerDeathAndOstyAttack_MatchSource(string scenario)
    {
        (Player player, CombatRoom room, OstyHookProbe probe) = await OstyFixture.EnterCombat($"osty-life-{scenario}");
        CombatState state = room.Engine.State;
        Creature enemy = state.HittableEnemies.Single();
        await OstyCmd.Summon(player, 10m, null);
        Creature osty = player.Osty!;

        switch (scenario)
        {
            case "player-death-kills-osty":
                await CreatureCmd.Kill(player.Creature);
                Assert.True(osty.IsDead);
                break;
            case "from-osty-attack":
            {
                var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
                card.AssignOwner(player);
                int enemyHp = enemy.CurrentHp;
                probe.Calls.Clear();
                var attack = await DamageCmd.Attack(6m).FromOsty(osty, card, null).Targeting(enemy).Execute();
                Assert.Same(osty, attack.Attacker);
                Assert.Equal(enemyHp - 6, enemy.CurrentHp);
                Assert.Contains("hit:enemy<-osty:StrikeRegent", probe.Calls);
                break;
            }
            case "dead-osty-unhittable":
                await CreatureCmd.Kill(osty);
                Assert.Contains(osty, state.Allies);
                Assert.Contains(osty, player.PlayerCombatState!.Pets);
                Assert.False(Hook.ShouldAllowHitting(state, osty));
                Assert.NotNull(osty.GetPower<DieForYouPower>());
                break;
            case "clone-isolation":
            {
                CombatState clone = state.Clone();
                Player clonePlayer = clone.Players[0];
                Creature cloneOsty = Assert.IsType<Creature>(clonePlayer.Osty);
                Assert.NotSame(osty, cloneOsty);
                Assert.Same(clonePlayer, cloneOsty.PetOwner);
                Assert.NotNull(cloneOsty.GetPower<DieForYouPower>());
                await CreatureCmd.Damage(clone, [clonePlayer.Creature], 15m, ValueProp.Move,
                    clone.HittableEnemies.Single(), null, null);
                Assert.True(cloneOsty.IsDead);
                Assert.Contains(cloneOsty, clone.Allies);
                Assert.Equal((10, 10), (osty.MaxHp, osty.CurrentHp));
                Assert.NotNull(osty.GetPower<DieForYouPower>());
                Assert.True(player.IsOstyAlive);
                break;
            }
        }
    }
}
