using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Entities.Relics;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Afflictions;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Enchantments;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.ValueProps;

namespace Sts2Sim.Core.Tests.Combat;

[Collection("ModelDb")]
public sealed class CardOwnerDeathWindowTests : IDisposable
{
    public CardOwnerDeathWindowTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes
            .Append(typeof(OwnerDeathStageCard))
            .Append(typeof(OwnerDeathStageRelic))
            .Append(typeof(OwnerDeathSentinelEnchantment))
            .Append(typeof(OwnerDeathStageAffliction)));
    }

    public void Dispose() => ModelDb.ResetForTests();

    [Theory]
    [InlineData("play-count")]
    [InlineData("effect")]
    [InlineData("enchantment")]
    [InlineData("affliction")]
    [InlineData("after-card")]
    [InlineData("before-card")]
    [InlineData("live")]
    public async Task OwnerDeathWindows(string stage)
    {
        const string seed = "owner-death-windows-815";
        var run = new RunState(seed, new Overgrowth());
        var player = Player.CreateForNewRun(ModelDb.Character<Silent>(), run);
        run.AddPlayer(player);
        var relic = (OwnerDeathStageRelic)ModelDb.Relic<OwnerDeathStageRelic>().MutableClone();
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
        var kusarigama = (Kusarigama)ModelDb.Relic<Kusarigama>().MutableClone();
        kusarigama.AssignOwner(player);
        player.AddRelicInternal(kusarigama);
        var observer = new OwnerDeathObserver();
        var room = new CombatRoom(
            () => (WanderingGrunt)ModelDb.Monster<WanderingGrunt>().MutableClone());
        // Configure once before the engine exists; only arm target metrics after warm-up.
        room.ConfigureObserver(observer);
        await room.EnterInternal(run);
        CombatState state = room.Engine.State;
        PlayerCombatState pcs = player.PlayerCombatState!;
        Creature enemy = Assert.Single(state.Enemies);
        foreach (CardModel existing in pcs.Hand.Cards.ToArray()) CardPileCmd.Remove(existing);
        pcs.Energy = 20;

        // Establish Kusarigama's first two counts through actual plays, not field injection.
        int targetsBeforeWarmup = run.Rng.CombatTargets.Counter;
        for (int index = 0; index < 2; index++)
        {
            var warmup = (StrikeSilent)ModelDb.Card<StrikeSilent>().MutableClone();
            warmup.AssignOwner(player);
            CardPileCmd.Add(warmup, PileType.Hand);
            Assert.NotNull(await warmup.PlayWithResultAsync(enemy));
        }
        Assert.Equal(2, pcs.AttackCardsPlayedThisTurn);
        Assert.Equal(targetsBeforeWarmup, run.Rng.CombatTargets.Counter);
        Assert.True(enemy.IsAlive && enemy.IsPrimaryEnemy);
        Assert.Contains(enemy, state.HittableEnemies);

        var card = (OwnerDeathStageCard)ModelDb.Card<OwnerDeathStageCard>().MutableClone();
        card.AssignOwner(player);
        card.Stage = stage;
        card.Observer = observer;
        CardPileCmd.Add(card, PileType.Hand);
        var sentinel = (OwnerDeathSentinelEnchantment)ModelDb
            .Get(typeof(OwnerDeathSentinelEnchantment)).MutableClone();
        Corrupted? corrupted = null;
        if (stage == "enchantment")
        {
            await CardCmd.Enchant<Corrupted>(card, 1m);
            corrupted = Assert.IsType<Corrupted>(Assert.Single(card.Enchantments));
            // Synthetic ordering fixture only: production intentionally permits one slot.
            // Bind the sentinel to the checked backing list, as HookListenerCacheTests does.
            // Never enchant again, clone, or save this deliberately synthetic two-slot card.
            var enchantments = Assert.IsType<List<EnchantmentModel>>(card.Enchantments);
            sentinel.AssignOwnerCard(card);
            enchantments.Add(sentinel);
            corrupted.ExecutionFinished += _ => observer.ObserveCorruptedCompletion();
        }
        else
        {
            await CardCmd.Enchant(sentinel, card, 1m);
        }
        var affliction = Assert.IsType<OwnerDeathStageAffliction>(
            await CardCmd.Afflict<OwnerDeathStageAffliction>(card, 1m));
        affliction.ExecutionFinished += _ => observer.ObserveAfflictionCompletion();
        card.MakeFreeUntilPlayed();
        card.SetToFreeThisTurn();
        player.Creature.SetCurrentHpInternal(stage == "live" ? 50 : 2);
        relic.Target = card;
        observer.Target = card;
        int startedBefore = pcs.CardPlaysStartedThisTurn;
        int completedBefore = pcs.CardsPlayedThisTurn;
        int etherealBefore = state.SemanticHistory.CountThisCombat(
            state, CombatSemanticHistory.ActorEvent.EtherealPlayFinished, player);
        int targetsBefore = run.Rng.CombatTargets.Counter;

        // Read immediately on return, before engine CheckWinCondition or room Exit cleanup.
        CardPlay? result = await card.PlayWithResultAsync(enemy);

        string[] expectedPhases = stage switch
        {
            "play-count" => ["count"],
            "effect" or "before-card" => ["count", "before", "effect"],
            "enchantment" => ["count", "before", "effect", "corrupted"],
            "affliction" => ["count", "before", "effect", "enchantment", "affliction"],
            "after-card" => ["count", "before", "effect", "enchantment", "affliction", "after"],
            _ => ["count", "before", "effect", "enchantment", "affliction", "after",
                "before", "effect", "enchantment", "affliction", "after"],
        };
        bool live = stage == "live";
        int expectedStarted = stage == "play-count" ? 0 : live ? 2 : 1;
        int expectedCompleted = live ? 2 : stage == "after-card" ? 1 : 0;
        var failures = new List<string>();
        void Check(bool condition, string message)
        {
            if (!condition) failures.Add(message);
        }
        Check(expectedPhases.SequenceEqual(observer.Phases),
            $"phase boundary: expected [{string.Join(',', expectedPhases)}], actual [{string.Join(',', observer.Phases)}]");
        Check(player.Creature.IsDead == !live, "owner final death");
        Check(enemy.IsAlive && state.HittableEnemies.Contains(enemy), "primary enemy must remain hittable");
        Check(pcs.CardPlaysStartedThisTurn - startedBefore == expectedStarted, "semantic started count");
        Check(pcs.CardsPlayedThisTurn - completedBefore == expectedCompleted, "semantic completed count");
        Check(state.SemanticHistory.CountThisCombat(state,
            CombatSemanticHistory.ActorEvent.EtherealPlayFinished, player) - etherealBefore == expectedCompleted,
            "Ethereal completed history");
        Check(card.Pile?.Type == (live ? PileType.Discard : PileType.Play),
            $"result-pile tail: actual {card.Pile?.Type}");
        Check(card.TemporaryFreeUntilPlayed == !live, "free-until-played tail");
        Check(card.TemporaryCostOverrideThisTurnOrUntilPlayed == (live ? (int?)null : 0), "energy override tail");
        Check(card.TemporaryStarCostOverrideThisTurn == (live ? (int?)null : 0), "star override tail");
        Check(card.LocalEnergyCost == (live ? 1 : 0), "local energy modifier cleanup");
        Check(relic.EmptyHandCalls == (live ? 1 : 0), "empty-hand callback boundary");
        Check(pcs.CardOrPotionEffectDepth == 0, "effect-depth finally");
        Check(observer.Started == 1 && observer.Finished == 1 && observer.Aborted == 0 && observer.Active == 0,
            $"diagnostic scope: {observer.Started}/{observer.Finished}/{observer.Aborted}/{observer.Active}");
        Check(ReferenceEquals(result, observer.FinishedPlay), "observer returned-play identity");
        Check(stage == "play-count" ? result is null :
            result is not null && ReferenceEquals(result, card.FirstPlay) && result.PlayIndex == 0,
            "firstPlay return identity");
        if (stage == "enchantment")
        {
            Check(corrupted is not null && observer.CorruptedMarkers == 1, "Corrupted lethal damage marker");
            Check(observer.CorruptedCompletions == 0, "Corrupted OnPlay completion after owner death");
            Check(run.Rng.CombatTargets.Counter - targetsBefore == 0, "Kusarigama consumed CombatTargets after Corrupted death");
        }
        if (stage == "affliction")
        {
            Check(observer.AfflictionMarkers == 1, "affliction lethal OnPlay marker");
            Check(observer.AfflictionCompletions == 0, "affliction OnPlay completion after owner death");
        }
        if (live)
            Check(run.Rng.CombatTargets.Counter - targetsBefore == 1, "live third attack must trigger Kusarigama");
        if (stage == "after-card")
            Check(run.Rng.CombatTargets.Counter - targetsBefore == 1,
                "AfterCardPlayed must finish later listeners even when its earlier listener kills the owner");
        // The empty-hand hook alone is not a RED discriminator: it independently skips dead owners.
        Assert.True(failures.Count == 0,
            $"seed={seed}, stage={stage}; owner-death boundary: {string.Join("; ", failures)}");
    }
}

file sealed class OwnerDeathStageCard : CardModel
{
    public string Stage { get; set; } = "";
    public OwnerDeathObserver Observer { get; set; } = null!;
    public CardPlay? FirstPlay { get; private set; }
    public override CardType Type => CardType.Attack;
    public override CardRarity Rarity => CardRarity.Token;
    public override TargetType TargetType => TargetType.AnyEnemy;
    protected override int CanonicalEnergyCost => 1;
    protected override int CanonicalStarCost => 2;
    protected override IReadOnlyCollection<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];

    protected override async Task OnPlay(CardPlay cardPlay)
    {
        FirstPlay ??= cardPlay;
        Observer.Phases.Add("effect");
        if (Stage == "effect") await KillOwner();
    }

    public Task KillOwner() => CreatureCmd.Damage(CombatState!, [Owner.Creature], 2m,
        ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move, null, this, FirstPlay);
}

file sealed class OwnerDeathStageRelic : RelicModel
{
    public OwnerDeathStageCard? Target { get; set; }
    public int EmptyHandCalls { get; private set; }
    public override RelicRarity Rarity => RelicRarity.Common;
    public override int ModifyCardPlayCount(CardModel card, Creature? target, int playCount) =>
        ReferenceEquals(card, Target) ? playCount + 1 : playCount;

    public override async Task AfterModifyingCardPlayCount(CardModel card)
    {
        if (!ReferenceEquals(card, Target)) return;
        Target.Observer.Phases.Add("count");
        if (Target.Stage == "play-count") await Target.KillOwner();
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (!ReferenceEquals(play.Card, Target)) return;
        Target.Observer.Phases.Add("before");
        if (Target.Stage == "before-card") await Target.KillOwner();
    }

    public override async Task AfterCardPlayed(CardPlay play)
    {
        if (!ReferenceEquals(play.Card, Target)) return;
        // Inventory precedes the Play pile: exclude generic Hook completion notifications.
        Target.Observer.CloseAfflictionWindow();
        Target.Observer.Phases.Add("after");
        if (Target.Stage == "after-card") await Target.KillOwner();
    }

    public override Task AfterHandEmptied(Player player)
    {
        if (ReferenceEquals(player, Owner) && Target?.Observer.Active == 1) EmptyHandCalls++;
        return Task.CompletedTask;
    }
}

file sealed class OwnerDeathSentinelEnchantment : EnchantmentModel
{
    public override Task OnPlay(CardModel card, CardPlay cardPlay)
    {
        var stageCard = (OwnerDeathStageCard)card;
        stageCard.Observer.CloseCorruptedWindow();
        stageCard.Observer.Phases.Add("enchantment");
        return Task.CompletedTask;
    }
}

file sealed class OwnerDeathStageAffliction : AfflictionModel
{
    public override async Task OnPlay(CardPlay cardPlay)
    {
        var card = (OwnerDeathStageCard)Card;
        card.Observer.Phases.Add("affliction");
        if (card.Stage != "affliction") return;
        await card.KillOwner();
        // Open only after nested damage/death Hook notifications have finished.
        card.Observer.OpenAfflictionWindow();
    }
}

file sealed class OwnerDeathObserver : ICombatObserver
{
    public OwnerDeathStageCard? Target { get; set; }
    public List<string> Phases { get; } = [];
    public int Started, Finished, Aborted, Active, CorruptedMarkers, CorruptedCompletions,
        AfflictionMarkers, AfflictionCompletions;
    public CardPlay? FinishedPlay;
    private bool _corruptedWindow, _afflictionWindow;
    public void CloseCorruptedWindow() => _corruptedWindow = false;
    public void CloseAfflictionWindow() => _afflictionWindow = false;
    public void ObserveCorruptedCompletion() { if (_corruptedWindow) CorruptedCompletions++; }
    public void ObserveAfflictionCompletion() { if (_afflictionWindow) AfflictionCompletions++; }
    public void OpenAfflictionWindow() { AfflictionMarkers++; _afflictionWindow = true; }
    public void DamageResolved(Creature? dealer, DamageResult result)
    {
        if (Active != 1 || Target?.Stage != "enchantment" ||
            !ReferenceEquals(result.Receiver, Target.Owner.Creature) || !result.Receiver.IsDead) return;
        // CreatureCmd reports after nested damage/death hooks, immediately before Corrupted returns.
        CorruptedMarkers++;
        Phases.Add("corrupted");
        _corruptedWindow = true;
    }
    public void CardPlayStarted(CardModel card, Creature? target)
    {
        if (!ReferenceEquals(card, Target)) return;
        Started++;
        Active++;
    }
    public void CardPlayFinished(CardModel card, Creature? target, CardPlay? cardPlay)
    {
        if (!ReferenceEquals(card, Target)) return;
        Finished++;
        Active--;
        FinishedPlay = cardPlay;
        CloseCorruptedWindow();
        CloseAfflictionWindow();
    }
    public void CardPlayAborted(CardModel card, Creature? target)
    {
        if (!ReferenceEquals(card, Target)) return;
        Aborted++;
        Active--;
        CloseCorruptedWindow();
        CloseAfflictionWindow();
    }
    public void CombatStarted(CombatState state) { }
    public void PlayerTurnStarted(CombatState state) { }
    public void CardDrawn(CardModel card) { }
    public void EnemyMoveStarted(Creature source, string moveId) { }
    public void EnemyMoveFinished(Creature source, string moveId) { }
    public void PlayerTurnEnded(CombatState state) { }
    public void PotionUseStarted(PotionModel potion, Creature? target) { }
    public void PotionUseFinished(PotionModel potion, Creature? target) { }
}
