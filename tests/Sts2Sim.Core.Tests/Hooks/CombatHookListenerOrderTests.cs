using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Hooks;

/// <summary>
/// #1072: the game's hook listener order (v0.113.1 <c>RunState.IterateHookListeners</c>,
/// <c>CombatState.IterateHookListeners</c>, <c>Hook.AfterCombatEnd</c>).
/// </summary>
[Collection("ModelDb")]
public sealed class CombatHookListenerOrderTests : IDisposable
{
    public CombatHookListenerOrderTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat([typeof(ShuffleProbeRelic)]));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("listener-order")]
    [InlineData("dampen-gremlin-horn")]
    [InlineData("combat-end-once")]
    public async Task Listeners_FollowGameOrder(string scenario)
    {
        var runState = new RunState("hook-order-" + scenario, new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        switch (scenario)
        {
            case "listener-order": await VerifyOrderAsync(runState, player); break;
            case "dampen-gremlin-horn": await VerifyDampenGremlinHornAsync(runState, player); break;
            case "combat-end-once": await VerifyCombatEndOnceAsync(runState, player); break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
    }

    private static async Task VerifyOrderAsync(RunState runState, Player player)
    {
        await RelicCmd.Obtain(ModelDb.Relic<GremlinHorn>(), player);
        PotionModel potion = player.AddPotionInternal(ModelDb.Potion<FirePotion>());
        CardModel[] deck = player.Deck.Cards.ToArray();
        Assert.NotEmpty(deck);

        // Out of combat: every deck card, then relics, then potions (game RunState with no child).
        AbstractModel[] outside = runState.IterateHookListeners(null).ToArray();
        Assert.Equal(deck, outside.Take(deck.Length));
        Assert.Equal(player.Relics.Cast<AbstractModel>().Append(potion), outside.Skip(deck.Length));

        CombatRoom room = NewRoom();
        await room.Enter(runState);
        CombatState combat = room.Engine.State;
        await PowerCmd.Apply<StrengthPower>(combat, player.Creature, 1m, player.Creature, null);
        PowerModel strength = player.Creature.GetPower<StrengthPower>()!;

        // In combat the run gives only deck cards, then the combat's listeners (creature by creature).
        AbstractModel[] inside = runState.IterateHookListeners(combat).ToArray();
        AbstractModel[] combatOnly = combat.IterateHookListeners().ToArray();
        Assert.Equal(deck, inside.Take(deck.Length));
        Assert.Equal(combatOnly, inside.Skip(deck.Length));
        Assert.DoesNotContain(combatOnly, model => deck.Contains(model));

        int Index(AbstractModel model) => Array.IndexOf(combatOnly, model);
        CardModel firstHand = player.PlayerCombatState!.Hand.Cards[0];
        Assert.True(Index(strength) < Index(player.Relics[0]), "player power before that player's relics");
        Assert.True(Index(player.Relics[^1]) < Index(potion), "relics before potions");
        Assert.True(Index(potion) < Index(firstHand), "potions before pile cards");
        Assert.True(Index(firstHand) < Index(combat.Enemies[0].Monster!), "allies before enemies");
    }

    private static async Task VerifyDampenGremlinHornAsync(RunState runState, Player player)
    {
        await RelicCmd.Obtain(ModelDb.Relic<GremlinHorn>(), player);
        await RelicCmd.Obtain(ModelDb.Relic<ShuffleProbeRelic>(), player);
        CombatRoom room = NewRoom();
        await room.Enter(runState);
        CombatState combat = room.Engine.State;
        Creature caster = await CreatureCmd.Add(
            (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone(), combat, CombatSide.Enemy, null);

        // Draw pile empty, so Gremlin Horn's draw reshuffles the discard pile.
        PlayerCombatState pcs = player.PlayerCombatState!;
        foreach (CardModel card in pcs.Hand.Cards.Concat(pcs.DrawPile.Cards).ToArray())
            CardPileCmd.Add(card, PileType.Discard);
        CardModel marked = pcs.DiscardPile.Cards[0];
        CardCmd.Upgrade(marked);
        await PowerCmd.Apply<DampenPower>(combat, player.Creature, 1m, caster, null);
        player.Creature.GetPower<DampenPower>()!.AddCaster(caster);
        Assert.Equal(0, marked.CurrentUpgradeLevel);
        var probe = player.Relics.OfType<ShuffleProbeRelic>().Single();
        probe.Marked = marked;

        await CreatureCmd.Damage(combat, new[] { caster }, 999m,
            ValueProp.Unblockable | ValueProp.Unpowered, player.Creature, null, null);

        Assert.False(combat.IsOverOrEnding());
        // Game AfterDeath order: the player's Dampen (a power) is removed and restores the upgrade before
        // Gremlin Horn (a relic) draws, so the reshuffle already sees the upgraded card.
        Assert.Equal(new[] { (false, 1) }, probe.Seen);
        Assert.Single(pcs.Hand.Cards);
    }

    private static async Task VerifyCombatEndOnceAsync(RunState runState, Player player)
    {
        CardModel deckCard = player.Deck.Cards[0];
        int finished = 0;
        deckCard.ExecutionFinished += _ => finished++;
        CombatRoom room = NewRoom();
        await room.Enter(runState);
        finished = 0;
        await Hook.AfterCombatEnd(room.Engine.State);
        Assert.True(finished == 1, $"deck card AfterCombatEnd dispatches={finished}; game Hook.AfterCombatEnd gives it once");
    }

    private static CombatRoom NewRoom() =>
        new(() => (MonsterModel)ModelDb.Monster<WanderingGrunt>().MutableClone());

    private sealed class ShuffleProbeRelic : RelicModel
    {
        internal CardModel? Marked { get; set; }
        internal List<(bool HadDampen, int MarkedLevel)> Seen { get; } = [];
        public override RelicRarity Rarity => RelicRarity.Common;

        public override Task AfterShuffle(Player player)
        {
            Seen.Add((player.Creature.GetPower<DampenPower>() is not null, Marked?.CurrentUpgradeLevel ?? -1));
            return Task.CompletedTask;
        }
    }
}
