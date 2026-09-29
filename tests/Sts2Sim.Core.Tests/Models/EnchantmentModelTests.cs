using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Random;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class EnchantmentModelTests : IDisposable
{
    public EnchantmentModelTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes.Concat(new[]
        {
            typeof(RegisteredEnchantment),
            typeof(OneShotEnchantment),
        }));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task Enchant_ClonesRegisteredCanonicalAndAttachesOwnerAndMagnitude()
    {
        var card = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        EnchantmentModel canonical = (EnchantmentModel)ModelDb.Get(typeof(RegisteredEnchantment));

        await CardCmd.Enchant<RegisteredEnchantment>(card, 3m);

        RegisteredEnchantment attached = Assert.IsType<RegisteredEnchantment>(
            Assert.Single(card.Enchantments));
        Assert.NotSame(canonical, attached);
        Assert.Same(card, attached.Owner);
        Assert.Equal(3m, attached.Magnitude);
    }

    [Fact]
    public async Task PlayAsync_RunsOneShotEnchantmentAfterCardAndBeforeAfterPlayedHook()
    {
        var trace = new List<string>();
        var listener = (TraceListener)new TraceListener(trace).MutableClone();
        (Player player, _) = CreatePlayer(listener);
        player.PlayerCombatState!.Energy = 10;
        var card = (TraceCard)new TraceCard(trace).MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState.Hand.AddInternal(card);
        await CardCmd.Enchant<OneShotEnchantment>(card, 1m);
        var enchantment = Assert.IsType<OneShotEnchantment>(card.Enchantments.Single());
        enchantment.Trace = trace;

        await card.PlayAsync(target: null);
        CardPileCmd.Add(card, PileType.Hand);
        await card.PlayAsync(target: null);

        Assert.Equal(1, enchantment.TriggerCount);
        Assert.Equal(EnchantmentStatus.Disabled, enchantment.Status);
        Assert.Equal(
            new[] { "card", "enchantment", "hook", "card", "hook" },
            trace);
    }

    [Fact]
    public async Task MutableClone_DeepClonesEnchantmentsAndRebindsTheirOwner()
    {
        var source = (StrikeRegent)ModelDb.Card<StrikeRegent>().MutableClone();
        await CardCmd.Enchant<RegisteredEnchantment>(source, 4m);

        var clone = (StrikeRegent)source.MutableClone();
        source.Enchantments[0].AssignMagnitude(9m);

        RegisteredEnchantment sourceEnchantment =
            Assert.IsType<RegisteredEnchantment>(Assert.Single(source.Enchantments));
        RegisteredEnchantment clonedEnchantment =
            Assert.IsType<RegisteredEnchantment>(Assert.Single(clone.Enchantments));
        Assert.NotSame(sourceEnchantment, clonedEnchantment);
        Assert.Same(source, sourceEnchantment.Owner);
        Assert.Same(clone, clonedEnchantment.Owner);
        Assert.Equal(9m, sourceEnchantment.Magnitude);
        Assert.Equal(4m, clonedEnchantment.Magnitude);
    }

    private static (Player player, ICombatState combatState) CreatePlayer(params AbstractModel[] listeners)
    {
        var runState = new FakeRunState(listeners);
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        player.ResetCombatState();
        var combatState = new FakeCombatState(runState);
        player.Creature.CombatState = combatState;
        return (player, combatState);
    }

    public sealed class RegisteredEnchantment : EnchantmentModel
    {
    }

    public sealed class OneShotEnchantment : EnchantmentModel
    {
        public int TriggerCount { get; private set; }

        public List<string> Trace { get; set; } = null!;

        public override Task OnPlay(CardModel card)
        {
            if (Status != EnchantmentStatus.Normal)
            {
                return Task.CompletedTask;
            }

            TriggerCount++;
            Status = EnchantmentStatus.Disabled;
            Trace.Add("enchantment");
            return Task.CompletedTask;
        }
    }

    private sealed class TraceCard(List<string> trace) : CardModel
    {
        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Common;

        public override TargetType TargetType => TargetType.Self;

        protected override int CanonicalEnergyCost => 0;

        protected override Task OnPlay(CardPlay cardPlay)
        {
            trace.Add("card");
            return Task.CompletedTask;
        }
    }

    private sealed class TraceListener(List<string> trace) : AbstractModel
    {
        public override bool ShouldReceiveCombatHooks => true;

        public override Task AfterCardPlayed(CardPlay cardPlay)
        {
            trace.Add("hook");
            return Task.CompletedTask;
        }

        public override Task AfterCardDrawn(CardModel card, bool fromHandDraw)
        {
            trace.Add("hook");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRunState(IReadOnlyList<AbstractModel> listeners) : IRunState
    {
        public Sts2Sim.Core.Rooms.AbstractRoom? CurrentRoom => null;
        public RunRngSet Rng { get; } = new("enchantment_model_tests");

        public Sts2Sim.Core.Entities.Ascension.AscensionManager Ascension { get; } = new(0);

        public IReadOnlyList<Player> Players => Array.Empty<Player>();
        public int TotalFloor => 0;

        public IEnumerable<AbstractModel> IterateHookListeners(ICombatState? childCombatState) => listeners;
    }

    private sealed class FakeCombatState(IRunState runState) : ICombatState
    {
        public IRunState RunState { get; } = runState;

        public IReadOnlyList<Creature> Allies => Array.Empty<Creature>();

        public IReadOnlyList<Creature> Enemies => Array.Empty<Creature>();

        public IReadOnlyList<Creature> Creatures => Array.Empty<Creature>();

        public IReadOnlyList<Player> Players => Array.Empty<Player>();

        public IReadOnlyList<Creature> HittableEnemies => Array.Empty<Creature>();

        public CombatSide CurrentSide { get; set; }

        public int RoundNumber { get; set; } = 1;

        public IEnumerable<AbstractModel> IterateHookListeners() => RunState.IterateHookListeners(this);

        public IReadOnlyList<Creature> GetOpponentsOf(Creature creature) => Array.Empty<Creature>();

        public IReadOnlyList<Creature> GetCreaturesOnSide(CombatSide side) => Array.Empty<Creature>();

        public bool ContainsCreature(Creature creature) => false;

        public bool IsLiveCombat() => true;
    }
}
