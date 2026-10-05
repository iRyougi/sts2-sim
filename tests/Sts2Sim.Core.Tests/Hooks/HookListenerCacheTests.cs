using System.Reflection;
using System.Runtime.InteropServices;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Powers;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Afflictions;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Orbs;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Hooks;

[Collection("ModelDb")]
public sealed class HookListenerCacheTests : IDisposable
{
    private delegate bool CostHook(CardModel card, decimal originalCost, out decimal modifiedCost);
    private delegate Task PileHook(CardModel card, PileType oldPileType, AbstractModel? clonedBy);
    private static readonly FieldInfo CacheField = typeof(CombatState).GetField(
        "_hookListenerSnapshotCache", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public HookListenerCacheTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(
            [typeof(BareRelic), typeof(DispatchRelic), typeof(GhostRelic), typeof(GhostPower)]));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("creatures-powers")]
    [InlineData("piles-attachments-orbs")]
    [InlineData("actual-run-wrappers")]
    [InlineData("phase-notifications-errors")]
    [InlineData("clone-isolation")]
    [InlineData("lifecycle-fallback")]
    public async Task Snapshot_PreservesExactSourcesAndDispatchBoundaries(string scenario)
    {
        switch (scenario)
        {
            case "creatures-powers": VerifyCreatureAliases(); break;
            case "piles-attachments-orbs": await VerifyPilesAsync(); break;
            case "actual-run-wrappers": await VerifyRunWrappersAsync(); break;
            case "phase-notifications-errors": await VerifyPhasesAsync(); break;
            case "clone-isolation": VerifyClones(); break;
            case "lifecycle-fallback": await VerifyLifecycleAsync(); break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
    }

    private static void VerifyCreatureAliases()
    {
        var world = NewWorld("creatures-powers");
        Creature first = world.State.Enemies[0];
        Creature second = world.State.CreateCreature(
            (MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy, "second");
        var enemies = (List<Creature>)world.State.Enemies;
        AssertMutation(world.State, world.Run, () => enemies.Add(second), "enemy alias Add");
        AssertMutation(world.State, world.Run, () => enemies.Reverse(), "enemy alias reorder");
        AssertMutation(world.State, world.Run, () => enemies[0] = first, "enemy alias indexer duplicate");
        AssertMutation(world.State, world.Run, () => enemies.RemoveAt(0), "enemy alias Remove");
        AssertMutation(world.State, world.Run, () => enemies.Insert(0, second), "enemy alias Insert");

        var strength = (StrengthPower)ModelDb.Power<StrengthPower>().MutableClone();
        strength.ApplyInternal(first, 1m);
        var weak = (WeakPower)ModelDb.Power<WeakPower>().MutableClone();
        weak.ApplyInternal(first, 1m);
        var replacement = (StrengthPower)ModelDb.Power<StrengthPower>().MutableClone();
        replacement.ApplyInternal(first, 1m);
        replacement.RemoveInternal();
        var powers = (List<PowerModel>)first.Powers;
        AssertMutation(world.State, world.Run, () => powers.Reverse(), "power alias reorder");
        AssertMutation(world.State, world.Run, () => powers[1] = replacement, "power alias same-type new reference");
        AssertMutation(world.State, world.Run, () => powers[0] = replacement, "power alias duplicate");
        AssertMutation(world.State, world.Run, () => powers.Add(weak), "power alias Add");
        AssertMutation(world.State, world.Run, () => powers.RemoveAt(0), "power alias Remove");
        AssertMutation(world.State, world.Run, () => powers.Clear(), "power alias Clear");

        var allies = (List<Creature>)world.State.Allies;
        AssertMutation(world.State, world.Run, () => allies.Add(world.Player.Creature), "ally duplicate");
        AssertMutation(world.State, world.Run, () => allies.RemoveAt(0), "ally Remove");
        AssertMutation(world.State, world.Run, world.Player.ResetCombatState, "PCS reference replaced");
        AssertMutation(world.State, world.Run, world.Player.DetachCombatState, "null PCS is a valid skip");
    }

    private static async Task VerifyPilesAsync()
    {
        var world = NewWorld("piles-attachments-orbs");
        PlayerCombatState pcs = world.Player.PlayerCombatState!;
        CardPile[] piles = [pcs.Hand, pcs.DrawPile, pcs.DiscardPile, pcs.ExhaustPile, pcs.PlayPile];
        for (int i = 0; i < piles.Length; i++)
        {
            CardPile pile = piles[i];
            CardModel first = NewCard(world.Player);
            CardModel second = (CardModel)ModelDb.Card<DefendRegent>().MutableClone();
            second.AssignOwner(world.Player);
            pile.AddInternal(first);
            pile.AddInternal(second);
            var cards = (List<CardModel>)pile.Cards;
            string label = $"pile={pile.Type}";
            AssertMutation(world.State, world.Run, () => cards.Add(first), label + " duplicate");
            AssertMutation(world.State, world.Run, () => cards.Sort((a, b) => ReferenceEquals(a, b)
                ? 0 : ReferenceEquals(a, second) ? -1 : 1), label + " Sort");
            AssertMutation(world.State, world.Run, () => CollectionsMarshal.AsSpan(cards)[0] = first,
                label + " span replacement without List version notification");
            AssertMutation(world.State, world.Run, () => cards.RemoveAt(0), label + " Remove");
        }

        CardModel attached = pcs.Hand.Cards[0];
        var before = Snapshot(world.State, world.Run, "before real enchant attach");
        await CardCmd.Enchant<Glam>(attached, 1m);
        AssertChanged(world.State, world.Run, before, "real enchant attach");
        var extra = (EnchantmentModel)ModelDb.Get(typeof(Sown)).MutableClone();
        extra.AssignOwnerCard(attached);
        var enchantments = (List<EnchantmentModel>)attached.Enchantments;
        AssertMutation(world.State, world.Run, () => enchantments.Add(extra), "multiple enchantments alias");
        AssertMutation(world.State, world.Run, () => enchantments.Reverse(), "enchantment order");
        AssertMutation(world.State, world.Run, () => enchantments[0] = enchantments[1], "enchantment duplicate");
        AssertMutation(world.State, world.Run, () => enchantments.Clear(), "enchantment clear alias");

        before = Snapshot(world.State, world.Run, "before real affliction attach");
        Assert.NotNull(await CardCmd.Afflict<Hexed>(attached, 1m));
        AssertChanged(world.State, world.Run, before, "real affliction attach");
        AssertMutation(world.State, world.Run, () => CardCmd.ClearAffliction(attached), "real affliction clear");

        var lightning = (OrbModel)ModelDb.Get(typeof(LightningOrb)).MutableClone();
        lightning.Owner = world.Player;
        var frost = (OrbModel)ModelDb.Get(typeof(FrostOrb)).MutableClone();
        frost.Owner = world.Player;
        var orbs = (List<OrbModel>)pcs.OrbQueue.Orbs;
        AssertMutation(world.State, world.Run, () => orbs.Add(lightning), "orb Add alias");
        AssertMutation(world.State, world.Run, () => orbs.Add(frost), "orb second member");
        AssertMutation(world.State, world.Run, () => orbs.Reverse(), "orb reorder");
        AssertMutation(world.State, world.Run, () => CollectionsMarshal.AsSpan(orbs)[0] = lightning,
            "orb span/duplicate");
        AssertMutation(world.State, world.Run, () => orbs.Clear(), "orb Clear alias");
    }

    private static async Task VerifyRunWrappersAsync()
    {
        var world = NewWorld("actual-run-wrappers");
        CombatState projection = world.State.Clone();
        foreach (CombatState state in new[] { world.State, projection })
        {
            IRunState root = state.RunState;
            Player player = root.Players[0];
            var relics = (List<RelicModel>)player.Relics;
            var extra = (BareRelic)ModelDb.Relic<BareRelic>().MutableClone();
            extra.AssignOwner(player);
            AssertMutation(state, root, () => relics.Add(extra), root.GetType().Name + " relic alias Add");
            AssertMutation(state, root, () => relics.Reverse(), "relic order");
            AssertMutation(state, root, () => extra.IsMelted = true, "live IsMelted true");
            AssertMutation(state, root, () => extra.IsMelted = false, "live IsMelted false");
            AssertMutation(state, root, () => relics[0] = relics[1], "relic indexer duplicate");

            PotionModel potion = player.AddPotionInternal(ModelDb.Potion<FirePotion>());
            var slots = (List<PotionModel?>)player.PotionSlots;
            AssertMutation(state, root, () => (slots[0], slots[1]) = (slots[1], slots[0]), "potion slot order");
            AssertMutation(state, root, () => slots[0] = potion, "potion alias duplicate");
            AssertMutation(state, root, () => slots[1] = null, "potion null slot");
            AssertMutation(state, root, () => slots.Add(null), "null slot length remains witnessed");

            var players = (List<Player>)root.Players;
            Player second = Player.CreateForNewRun(ModelDb.Character<Regent>(), root);
            AssertMutation(state, root, () => players.Add(second), "actual run Players alias Add");
            AssertMutation(state, root, () => players.Reverse(), "actual run Players order");
        }

        var other = NewWorld("different-actual-run-root");
        var previous = Snapshot(world.State, world.Run, "combat's own root");
        var explicitSnapshot = Snapshot(world.State, other.Run, "explicit different root");
        Assert.NotSame(previous, explicitSnapshot);
        Assert.True(explicitSnapshot.Version > previous.Version, "explicit root replacement must invalidate");
        Assert.Contains(explicitSnapshot.Full, model => ReferenceEquals(model, other.Player.Relics[0]));
        Assert.DoesNotContain(explicitSnapshot.Full, model => ReferenceEquals(model, world.Player.Relics[0]));
        Assert.Null(world.State.GetHookListenerSnapshot(null!, CombatState.HookListenerPhase.AfterPiles));

        // No-child dispatch stays the real run/deck path and does not touch combat's cache.
        var deckWorld = NewWorld("no-child-deck");
        var cached = Snapshot(deckWorld.State, deckWorld.Run, "before no-child dispatch");
        AbstractModel[] full = deckWorld.Run.IterateHookListeners(null).ToArray();
        var finished = new List<AbstractModel>();
        foreach (AbstractModel listener in full) listener.ExecutionFinished += model => finished.Add(model);
        await Hook.AfterCardChangedPiles(deckWorld.Run, null, NewCard(deckWorld.Player), PileType.Hand, null);
        AssertReferences(full.Concat(full).ToArray(), finished, "no-child finished deck order");
        Assert.Same(cached, CacheField.GetValue(deckWorld.State));
    }

    private static async Task VerifyPhasesAsync()
    {
        var world = NewWorld("phase-notifications-errors");
        var relics = (List<RelicModel>)world.Player.Relics;
        relics.Clear();
        var log = new List<string>();
        var bare = NewRelic<BareRelic>(world.Player);
        var a = NewRelic<DispatchRelic>(world.Player);
        var b = NewRelic<DispatchRelic>(world.Player);
        var c = NewRelic<DispatchRelic>(world.Player);
        var d = NewRelic<DispatchRelic>(world.Player);
        relics.AddRange([bare, a, b]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bare.ExecutionFinished += _ => log.Add("base:finished");
        a.Normal = async () => { log.Add("a:start"); entered.SetResult(); await release.Task; log.Add("a:resume"); };
        a.Late = () => { log.Add("a:late"); return Task.CompletedTask; };
        bool added = false;
        a.ExecutionFinished += _ => { log.Add("a:finished"); if (!added) { added = true; relics.Add(d); } };
        b.Normal = () => { log.Add("b:normal"); return Task.CompletedTask; };
        b.ExecutionFinished += _ => log.Add("b:finished");
        c.Normal = () => throw new InvalidOperationException("c must not enter the in-flight normal snapshot");
        c.Late = () => { log.Add("c:late"); return Task.CompletedTask; };
        c.ExecutionFinished += _ => log.Add("c:finished");
        d.Normal = () => throw new InvalidOperationException("d must not enter the in-flight normal snapshot");
        d.Late = () => { log.Add("d:late"); return Task.CompletedTask; };
        d.ExecutionFinished += _ => log.Add("d:finished");
        var inFlight = Snapshot(world.State, world.Run, "normal captured before await");
        AbstractModel[] captured = inFlight.Full.ToArray();
        Task dispatch = Hook.AfterCardChangedPiles(world.Run, world.State, NewCard(world.Player), PileType.Hand, null);
        await entered.Task;
        Assert.Equal(new[] { "base:finished", "a:start" }, log);
        relics.Remove(b);
        relics.Add(c);
        release.SetResult();
        await dispatch;
        Assert.Equal(new[] { "base:finished", "a:start", "a:resume", "a:finished", "b:normal", "b:finished",
            "base:finished", "a:late", "a:finished", "c:late", "c:finished", "d:late", "d:finished" }, log);
        AssertReferences(captured, inFlight.Full, "await/finished cannot mutate an in-flight array");
        AssertChanged(world.State, world.Run, inFlight, "late observes await and finished inventory mutations");

        var malformed = NewWorld("null-listener-timing");
        ((List<RelicModel>)malformed.Player.Relics).Clear();
        var beforeNull = NewRelic<DispatchRelic>(malformed.Player);
        malformed.Player.AddRelicInternal(beforeNull);
        var effects = new List<string>();
        beforeNull.Normal = () => { effects.Add("hook"); return Task.CompletedTask; };
        beforeNull.Late = () => { effects.Add("late"); return Task.CompletedTask; };
        beforeNull.ExecutionFinished += _ => effects.Add("finished");
        beforeNull.Energy = cost => { effects.Add("cost"); return cost + 1m; };
        ((List<PowerModel>)malformed.Player.Creature.Powers).Add(null!);
        Assert.Null(malformed.State.GetHookListenerSnapshot(malformed.Run, CombatState.HookListenerPhase.AfterPiles));
        await Assert.ThrowsAsync<NullReferenceException>(() => Hook.AfterCardChangedPiles(
            malformed.Run, malformed.State, NewCard(malformed.Player), PileType.Hand, null));
        Assert.Equal(new[] { "hook", "finished" }, effects);
        effects.Clear();
        Assert.Throws<NullReferenceException>(() => Hook.ModifyEnergyCostInCombat(
            malformed.State, NewCard(malformed.Player), 1m, out _));
        Assert.Empty(effects); // Cost Where/ToArray fails before any cost Hook.
        ((List<Creature>)malformed.State.Enemies).Add(null!);
        await Assert.ThrowsAsync<NullReferenceException>(() => Hook.AfterCardChangedPiles(
            malformed.Run, malformed.State, NewCard(malformed.Player), PileType.Hand, null));
        Assert.Empty(effects); // A null creature fails eager construction, before the first Hook.

        foreach (bool throwFromFinished in new[] { false, true })
        {
            var failure = NewWorld("throw-boundary-" + throwFromFinished);
            ((List<RelicModel>)failure.Player.Relics).Clear();
            var first = NewRelic<DispatchRelic>(failure.Player);
            var later = NewRelic<DispatchRelic>(failure.Player);
            failure.Player.AddRelicInternal(first);
            failure.Player.AddRelicInternal(later);
            var calls = new List<string>();
            var error = new InvalidOperationException("approved hook/finished boundary");
            first.Normal = () => { calls.Add("hook"); if (!throwFromFinished) throw error; return Task.CompletedTask; };
            first.ExecutionFinished += _ => { calls.Add("finished"); throw error; };
            later.Normal = () => { calls.Add("later"); return Task.CompletedTask; };
            InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                Hook.AfterCardChangedPiles(failure.Run, failure.State, NewCard(failure.Player), PileType.Hand, null));
            Assert.Same(error, actual);
            Assert.Equal(throwFromFinished ? new[] { "hook", "finished" } : new[] { "hook" }, calls);
        }
    }

    private static void VerifyClones()
    {
        var world = NewWorld("clone-isolation");
        world.Player.PlayerCombatState!.Hand.AddInternal(NewCard(world.Player));
        string coldProjection = Sts2Sim.Core.Tests.Runs.RunCloneGraphSnapshot.Capture(world.Run);
        var source = Snapshot(world.State, world.Run, "source warm");
        Assert.Equal(coldProjection, Sts2Sim.Core.Tests.Runs.RunCloneGraphSnapshot.Capture(world.Run));
        CombatState child = world.State.Clone();
        CombatState sibling = world.State.Clone();
        Assert.Null(CacheField.GetValue(child));
        Assert.Null(CacheField.GetValue(sibling));
        var childSnapshot = Snapshot(child, child.RunState, "child owns private wrapper");
        var siblingSnapshot = Snapshot(sibling, sibling.RunState, "sibling owns private wrapper");
        Assert.NotSame(source.Full, childSnapshot.Full);
        Assert.NotSame(childSnapshot.Full, siblingSnapshot.Full);
        foreach (FieldInfo field in typeof(CombatState.HookListenerSnapshot).GetFields(
                     BindingFlags.Instance | BindingFlags.NonPublic).Where(field => field.FieldType.IsArray))
        {
            Assert.NotSame(field.GetValue(source), field.GetValue(childSnapshot));
            Assert.NotSame(field.GetValue(childSnapshot), field.GetValue(siblingSnapshot));
        }
        Assert.False(childSnapshot.Full.Any(model => source.Full.Any(original => ReferenceEquals(original, model))),
            "child listener references must all belong to its own world");
        Assert.False(siblingSnapshot.Full.Any(model => childSnapshot.Full.Any(other => ReferenceEquals(other, model))),
            "siblings must not share mutable listener references");
        AssertMutation(child, child.RunState, () => ((List<PowerModel>)child.Players[0].Creature.Powers)
            .Add((PowerModel)ModelDb.Power<StrengthPower>().MutableClone()), "child-only alias mutation");
        Assert.Same(source, Snapshot(world.State, world.Run, "source unchanged by child"));
        Assert.Same(siblingSnapshot, Snapshot(sibling, sibling.RunState, "sibling unchanged by child"));

        var ghost = (GhostPower)ModelDb.Power<GhostPower>().MutableClone();
        ghost.ApplyInternal(world.Player.Creature, 1m);
        var foreign = NewWorld("old-cache-actual-run-root");
        var foreignGhost = NewRelic<GhostRelic>(foreign.Player);
        foreign.Player.AddRelicInternal(foreignGhost);
        var stale = Snapshot(world.State, foreign.Run, "cache retains old explicit root and listener");
        ghost.RemoveInternal();
        Assert.Contains(ghost, stale.Full);
        int powerClones = GhostPower.CloneCount;
        int relicClones = GhostRelic.CloneCount;
        string sourceProjection = Sts2Sim.Core.Tests.Runs.RunCloneGraphSnapshot.Capture(world.Run);
        RunState stableRun = world.Run.CloneExact();
        Assert.Equal(powerClones, GhostPower.CloneCount);
        Assert.Equal(relicClones, GhostRelic.CloneCount);
        Assert.Equal(sourceProjection, Sts2Sim.Core.Tests.Runs.RunCloneGraphSnapshot.Capture(stableRun));
        var stable = Assert.IsType<CombatState>(stableRun.Players[0].Creature.CombatState);
        Assert.Null(CacheField.GetValue(stable));
        Assert.Same(stale, CacheField.GetValue(world.State)); // Clone itself must not refresh source.
        var stableSnapshot = Snapshot(stable, stableRun, "stable run child warm from current own graph");
        Assert.DoesNotContain(stableSnapshot.Full, model => model is GhostPower or GhostRelic);
        var refreshed = Snapshot(world.State, world.Run, "source returns to its actual current root");
        Assert.DoesNotContain(refreshed.Full, model => model is GhostPower or GhostRelic);
        Assert.NotSame(refreshed.Full, stableSnapshot.Full);
    }

    private static async Task VerifyLifecycleAsync()
    {
        var world = NewWorld("lifecycle-fallback");
        ((List<RelicModel>)world.Player.Relics).Clear();
        var probe = NewRelic<DispatchRelic>(world.Player);
        world.Player.AddRelicInternal(probe);
        var log = new List<string>();
        probe.Energy = cost => { log.Add("energy"); return cost + 1m; };
        probe.EnergyLate = cost => { log.Add("energy-late"); return cost + 10m; };
        probe.Star = cost => { log.Add("star"); return cost + 100m; };
        CardModel card = NewCard(world.Player);
        var cached = Snapshot(world.State, world.Run, "cache before lifecycle transitions");
        var engine = new CombatEngine(world.State);
        Assert.Equal(1m, Hook.ModifyEnergyCostInCombat(world.State, card, 1m, out bool modified));
        Assert.False(modified);
        Assert.Empty(log);
        await engine.StartCombatAsync(() =>
        {
            Assert.True(world.State.IsStarting());
            Assert.True(world.State.IsOverOrEnding());
            Assert.Equal(12m, Hook.ModifyEnergyCostInCombat(world.State, card, 1m, out bool changed));
            Assert.True(changed);
            Assert.Equal(101m, Hook.ModifyStarCostInCombat(world.State, card, 1m, out changed));
            Assert.True(changed);
            return Task.CompletedTask;
        });
        log.Clear();
        Assert.Equal(12m, Hook.ModifyEnergyCostInCombat(world.State, card, 1m, out modified));
        Assert.True(modified);
        Assert.Equal(new[] { "energy", "energy-late" }, log);
        Snapshot(world.State, world.Run, "live PCS/piles have their own witness");
        probe.Energy = cost => { log.Add("lethal-normal"); world.State.Enemies[0].SetCurrentHpInternal(0); return cost + 1m; };
        log.Clear();
        Assert.Equal(2m, Hook.ModifyEnergyCostInCombat(world.State, card, 1m, out modified));
        Assert.True(modified);
        Assert.Equal(new[] { "lethal-normal" }, log); // Late rechecks gate after normal effects.
        Assert.True(world.State.IsEnding());
        log.Clear();
        Assert.Equal(1m, Hook.ModifyStarCostInCombat(world.State, card, 1m, out modified));
        Assert.False(modified);
        int finished = 0;
        probe.Normal = () => { log.Add("piles"); return Task.CompletedTask; };
        probe.Late = () => { log.Add("piles-late"); return Task.CompletedTask; };
        probe.ExecutionFinished += _ => finished++;
        await Hook.AfterCardChangedPiles(world.Run, world.State, card, PileType.Hand, null);
        Assert.Equal(new[] { "piles", "piles-late" }, log);
        Assert.Equal(2, finished); // AfterPiles remains ungated while ending.
        Assert.True(engine.CheckWinCondition());
        log.Clear();
        Assert.Equal(1m, Hook.ModifyEnergyCostInCombat(world.State, card, 1m, out modified));
        Assert.False(modified);
        Assert.Empty(log);
        await Hook.AfterCardChangedPiles(world.Run, world.State, card, PileType.Hand, null);
        Assert.Equal(new[] { "piles", "piles-late" }, log);
        Assert.Equal(4, finished);

        probe.Energy = cost => cost + 1m;
        probe.EnergyLate = cost => cost + 10m;
        var unknown = new UnknownRunState(world.Run, [probe]);
        Assert.Null(world.State.GetHookListenerSnapshot(unknown, CombatState.HookListenerPhase.Energy));
        Assert.Equal(0, unknown.Enumerations);
        var facade = new UnknownCombatState(world.State, unknown) { Over = true };
        Assert.Equal(1m, Hook.ModifyEnergyCostInCombat(facade, card, 1m, out modified));
        Assert.False(modified);
        Assert.Equal(0, unknown.Enumerations);
        Assert.Equal(2, facade.StartingReads);
        facade.Starting = true;
        Assert.Equal(12m, Hook.ModifyEnergyCostInCombat(facade, card, 1m, out modified));
        Assert.True(modified);
        Assert.Equal(2, unknown.Enumerations);
        facade.Over = false;
        facade.Starting = false;
        int startingReads = facade.StartingReads;
        Assert.Equal(12m, Hook.ModifyEnergyCostInCombat(facade, card, 1m, out modified));
        Assert.True(modified);
        Assert.Equal(startingReads, facade.StartingReads); // Original && short-circuits.
        await Hook.AfterCardChangedPiles(unknown, facade, card, PileType.Hand, null);
        Assert.Equal(6, unknown.Enumerations); // Two independent enumerations per dispatch.
        Assert.NotSame(cached, CacheField.GetValue(world.State)); // Real setup changed the source graph.
    }

    private static (RunState Run, Player Player, CombatState State) NewWorld(string label)
    {
        var run = new RunState("hook-listener-cache-" + label, new Overgrowth(), 10);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), run);
        run.AddPlayer(player);
        player.ResetCombatState();
        var state = new CombatState(run);
        state.AddPlayerCreature(player.Creature);
        state.AddMonster((MonsterModel)ModelDb.Monster<TrainingDummy>().MutableClone(), CombatSide.Enemy);
        return (run, player, state);
    }

    private static CardModel NewCard(Player player)
    {
        var card = (CardModel)ModelDb.Card<StrikeRegent>().MutableClone();
        card.AssignOwner(player);
        return card;
    }

    private static T NewRelic<T>(Player player) where T : RelicModel
    {
        var relic = (T)ModelDb.Relic<T>().MutableClone();
        relic.AssignOwner(player);
        return relic;
    }

    private static CombatState.HookListenerSnapshot Snapshot(CombatState state, IRunState root, string label)
    {
        var snapshot = state.GetHookListenerSnapshot(root, CombatState.HookListenerPhase.AfterPiles);
        Assert.True(snapshot is not null, label + ": known valid source must cache");
        AbstractModel[] full = root.IterateHookListeners(state).ToArray();
        AssertReferences(full, snapshot!.Full, label + ": original full wrapper");
        foreach (CombatState.HookListenerPhase phase in Enum.GetValues<CombatState.HookListenerPhase>())
            Assert.Same(snapshot, state.GetHookListenerSnapshot(root, phase));
        foreach (HookOverrideIndex.CostSlot slot in new[] { HookOverrideIndex.CostSlot.Energy,
                     HookOverrideIndex.CostSlot.EnergyLate, HookOverrideIndex.CostSlot.Star })
            AssertReferences(full.Where(model => RuntimeOverride(model, slot.ToString())).ToArray(),
                snapshot.CostListeners(slot), label + ": runtime base-slot oracle " + slot);
        for (int i = 0; i < full.Length; i++)
        {
            Assert.Equal(RuntimeOverride(full[i], "AfterPiles"), snapshot.OverridesPile(i, HookOverrideIndex.PileSlot.Normal));
            Assert.Equal(RuntimeOverride(full[i], "AfterPilesLate"), snapshot.OverridesPile(i, HookOverrideIndex.PileSlot.Late));
        }
        return snapshot;
    }

    private static bool RuntimeOverride(AbstractModel model, string slot)
    {
        bool pile = slot is "AfterPiles" or "AfterPilesLate";
        string method = slot switch
        {
            "Energy" => nameof(AbstractModel.TryModifyEnergyCostInCombat),
            "EnergyLate" => nameof(AbstractModel.TryModifyEnergyCostInCombatLate),
            "Star" => nameof(AbstractModel.TryModifyStarCostInCombat),
            "AfterPiles" => nameof(AbstractModel.AfterCardChangedPiles),
            "AfterPilesLate" => nameof(AbstractModel.AfterCardChangedPilesLate),
            _ => throw new ArgumentOutOfRangeException(nameof(slot)),
        };
        Type[] parameters = pile ? [typeof(CardModel), typeof(PileType), typeof(AbstractModel)]
            : [typeof(CardModel), typeof(decimal), typeof(decimal).MakeByRefType()];
        MethodInfo baseSlot = typeof(AbstractModel).GetMethod(method, BindingFlags.Public | BindingFlags.Instance,
            binder: null, parameters, modifiers: null)!;
        Delegate bound = Delegate.CreateDelegate(pile ? typeof(PileHook) : typeof(CostHook), model, baseSlot);
        return bound.Method.DeclaringType != typeof(AbstractModel);
    }

    private static void AssertMutation(CombatState state, IRunState root, Action mutate, string label)
    {
        var before = Snapshot(state, root, label + " before");
        AbstractModel[] captured = before.Full.ToArray();
        mutate();
        AssertChanged(state, root, before, label);
        AssertReferences(captured, before.Full, label + ": previous in-flight array immutable");
    }

    private static void AssertChanged(CombatState state, IRunState root,
        CombatState.HookListenerSnapshot before, string label)
    {
        var after = Snapshot(state, root, label + " after");
        Assert.NotSame(before, after);
        Assert.True(after.Version > before.Version, label + ": source mismatch must advance observed version");
    }

    private static void AssertReferences(IReadOnlyList<AbstractModel> expected,
        IReadOnlyList<AbstractModel> actual, string label)
    {
        Assert.True(expected.Count == actual.Count, $"{label}: count expected={expected.Count}, actual={actual.Count}");
        for (int i = 0; i < expected.Count; i++)
            Assert.True(ReferenceEquals(expected[i], actual[i]),
                $"{label}: first unequal reference index={i}; expected={expected[i]?.GetType().FullName}, actual={actual[i]?.GetType().FullName}");
    }

    private sealed class BareRelic : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Common;
    }

    private sealed class DispatchRelic : RelicModel
    {
        public override RelicRarity Rarity => RelicRarity.Common;
        internal Func<Task>? Normal { get; set; }
        internal Func<Task>? Late { get; set; }
        internal Func<decimal, decimal>? Energy { get; set; }
        internal Func<decimal, decimal>? EnergyLate { get; set; }
        internal Func<decimal, decimal>? Star { get; set; }
        public override Task AfterCardChangedPiles(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
            Normal?.Invoke() ?? Task.CompletedTask;
        public override Task AfterCardChangedPilesLate(CardModel card, PileType oldPileType, AbstractModel? clonedBy) =>
            Late?.Invoke() ?? Task.CompletedTask;
        public override bool TryModifyEnergyCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
        { modifiedCost = Energy?.Invoke(originalCost) ?? originalCost; return Energy is not null; }
        public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
        { modifiedCost = EnergyLate?.Invoke(originalCost) ?? originalCost; return EnergyLate is not null; }
        public override bool TryModifyStarCostInCombat(CardModel card, decimal originalCost, out decimal modifiedCost)
        { modifiedCost = Star?.Invoke(originalCost) ?? originalCost; return Star is not null; }
    }

    private sealed class GhostPower : PowerModel
    {
        internal static int CloneCount;
        public override PowerType Type => PowerType.Buff;
        public override PowerStackType StackType => PowerStackType.Counter;
        protected override void DeepCloneFields() { base.DeepCloneFields(); CloneCount++; }
    }

    private sealed class GhostRelic : RelicModel
    {
        internal static int CloneCount;
        public override RelicRarity Rarity => RelicRarity.Common;
        protected override void DeepCloneFields() { base.DeepCloneFields(); CloneCount++; }
    }

    private sealed class UnknownRunState(IRunState source, AbstractModel[] listeners) : IRunState
    {
        internal int Enumerations { get; private set; }
        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState)
        { Enumerations++; return listeners; }
        public RunRngSet Rng => source.Rng;
        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension => source.Ascension;
        public IReadOnlyList<Player> Players => throw new InvalidOperationException("Unknown wrapper witness must not read Players");
        public int TotalFloor => source.TotalFloor;
        public AbstractRoom? CurrentRoom => source.CurrentRoom;
    }

    private sealed class UnknownCombatState(CombatState source, IRunState root) : ICombatState
    {
        internal bool Over { get; set; }
        internal bool Starting { get; set; }
        internal int StartingReads { get; private set; }
        public IEnumerable<AbstractModel> IterateHookListeners() => source.IterateHookListeners();
        public IRunState RunState => root;
        public IReadOnlyList<Creature> Allies => source.Allies;
        public IReadOnlyList<Creature> Enemies => source.Enemies;
        public IReadOnlyList<Creature> Creatures => source.Creatures;
        public IReadOnlyList<Player> Players => source.Players;
        public IReadOnlyList<Creature> HittableEnemies => source.HittableEnemies;
        public CombatSide CurrentSide { get => source.CurrentSide; set => source.CurrentSide = value; }
        public int RoundNumber { get => source.RoundNumber; set => source.RoundNumber = value; }
        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => source.GetOpponentsOf(creature);
        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) => source.GetCreaturesOnSide(side);
        public bool ContainsCreature(Creature creature) => source.ContainsCreature(creature);
        public bool IsLiveCombat() => !Over;
        public bool IsOverOrEnding() => Over;
        public bool IsStarting() { StartingReads++; return Starting; }
    }
}
