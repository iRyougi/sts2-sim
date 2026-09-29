using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Tests.Models;

[Collection("ModelDb")]
public sealed class TurnEndInHandEffectTests : IDisposable
{
    public TurnEndInHandEffectTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(new[]
        {
            typeof(Regent),
            typeof(StrikeRegent),
            typeof(DefendRegent),
            typeof(FallingStar),
            typeof(Venerate),
            typeof(Sts2Sim.Core.Models.Powers.WeakPower),
            typeof(Sts2Sim.Core.Models.Powers.VulnerablePower),
            typeof(Sts2Sim.Core.Models.Relics.DivineRight),
        });
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Fact]
    public async Task BeforePlayerSideTurnEnd_WhileInHand_TriggersRetainedEffect()
    {
        (Player player, ICombatState combatState) = CreateContext();
        var card = (TurnEndInHandProbeCard)new TurnEndInHandProbeCard().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);

        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Player, new[] { player.Creature });
        await CombatEngine.DoTurnEndAsync(combatState, player);

        Assert.Equal(1, card.TriggerCount);
        // 原版 DoTurnEndCards：结算后放到弃牌堆底。
        Assert.Contains(card, player.PlayerCombatState.DiscardPile.Cards);
    }

    [Theory]
    [InlineData(PileType.Discard)]
    [InlineData(PileType.Draw)]
    [InlineData(PileType.Exhaust)]
    public async Task BeforePlayerSideTurnEnd_OutsideHand_DoesNotTriggerRetainedEffect(PileType pileType)
    {
        (Player player, ICombatState combatState) = CreateContext();
        var card = (TurnEndInHandProbeCard)new TurnEndInHandProbeCard().MutableClone();
        card.AssignOwner(player);
        CardPile pile = GetCombatPile(player, pileType);
        pile.AddInternal(card);

        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Player, new[] { player.Creature });
        await CombatEngine.DoTurnEndAsync(combatState, player);

        Assert.Equal(0, card.TriggerCount);
    }

    [Fact]
    public async Task BeforeEnemySideTurnEnd_WhileInHand_DoesNotTriggerRetainedEffect()
    {
        (Player player, ICombatState combatState) = CreateContext();
        var card = (TurnEndInHandProbeCard)new TurnEndInHandProbeCard().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);

        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Enemy, new[] { player.Creature });

        Assert.Equal(0, card.TriggerCount);
    }

    [Fact]
    public async Task BeforePlayerSideTurnEnd_DefaultRetainedEffectFlag_DoesNotTrigger()
    {
        (Player player, ICombatState combatState) = CreateContext();
        var card = (DefaultTurnEndInHandProbeCard)new DefaultTurnEndInHandProbeCard().MutableClone();
        card.AssignOwner(player);
        player.PlayerCombatState!.Hand.AddInternal(card);

        await Hook.BeforeSideTurnEnd(combatState, CombatSide.Player, new[] { player.Creature });
        await CombatEngine.DoTurnEndAsync(combatState, player);

        Assert.Equal(0, card.TriggerCount);
    }

    private static (Player Player, ICombatState CombatState) CreateContext()
    {
        var runState = new RunState("turn-end-in-hand-effect-tests", new Overgrowth());
        Player player = Player.CreateForNewRun(ModelDb.Character<Regent>(), runState);
        runState.AddPlayer(player);
        player.ResetCombatState();
        var combatState = new CombatState(runState);
        combatState.AddPlayerCreature(player.Creature);
        return (player, combatState);
    }

    private static CardPile GetCombatPile(Player player, PileType pileType) =>
        CardPileCmd.Get(pileType, player)
        ?? throw new InvalidOperationException($"Expected {pileType} combat pile.");

    private sealed class TurnEndInHandProbeCard : CardModel
    {
        public int TriggerCount { get; private set; }

        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.None;

        protected override int CanonicalEnergyCost => 0;

        protected override bool HasTurnEndInHandEffect => true;

        protected override Task OnTurnEndInHand()
        {
            TriggerCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class DefaultTurnEndInHandProbeCard : CardModel
    {
        public int TriggerCount { get; private set; }

        public override CardType Type => CardType.Skill;

        public override CardRarity Rarity => CardRarity.Basic;

        public override TargetType TargetType => TargetType.None;

        protected override int CanonicalEnergyCost => 0;

        protected override Task OnTurnEndInHand()
        {
            TriggerCount++;
            return Task.CompletedTask;
        }
    }
}
