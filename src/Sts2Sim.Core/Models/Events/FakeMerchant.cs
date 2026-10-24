using System.Collections.Immutable;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Entities.Merchant;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Hooks;
using Sts2Sim.Core.Models.Encounters;
using Sts2Sim.Core.Models.Potions;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Rooms;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

/// <summary>偏离 #284：Godot商人按钮/库存窗口由 IsInventoryOpen 与显式 Buy/Leave 操作表达。</summary>
public sealed class FakeMerchant : EventModel
{
    private SemaphoreSlim _interactionGate = new(1, 1);
    private IReadOnlyList<Reward> _combatRewards = [];
    public MerchantInventory Inventory { get; private set; } = null!;
    public bool IsInventoryOpen { get; set; }
    public bool StartedFight { get; private set; }
    protected override bool UsesCustomInteraction => true;
    public override bool GenerateForcedCombatRewards => true;
    public override int? ForcedCombatGold => FakeMerchantEventEncounter.GoldReward;
    public override IReadOnlyList<Reward> ForcedCombatExtraRewards => _combatRewards;
    public bool IsMerchantTargetAvailable => !IsFinished && !StartedFight && !IsInventoryOpen &&
        RunState.CurrentRoom is EventRoom room && ReferenceEquals(room.Event, this);

    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: >= 1 } &&
        runState.Players.Count == 1 && runState.Players.All(p => p.Gold >= 100 || p.PotionSlots.Any(x => x is FoulPotion));

    // The native current page contains no dynamic placeholders.
    protected override VisibleDecisionMetadata BuildVisibleState() => VisibleDecisionMetadata.Empty;

    protected override VisibleDecisionMetadata BuildCustomVisibleState(IReadOnlyList<CustomEventDecision> candidates)
    {
        var context = ImmutableArray.CreateBuilder<VisibleValue>();
        var gaps = ImmutableArray.CreateBuilder<string>();
        // Opening native inventory is free, reversible UI: stock is visible from event entry.
        // Include unaffordable and already purchased entries independently of legal actions.
        foreach (MerchantRelicEntry entry in Inventory.Relics)
        {
            string prefix = "Stock." + entry.Relic.Id.Entry;
            context.Add(VisibleValue.Int(prefix + ".Price", entry.Price));
            context.Add(VisibleValue.Condition(prefix + ".Purchased", entry.Purchased ? "true" : "false"));
            AddContent(prefix, VisibleContentProjection.ForRelic(entry.Relic));
        }
        return new(context.ToImmutable(), candidates.Select(candidate => candidate switch
        {
            CustomEventDecision.BuyRelic buy => new VisibleOption("BUY_RELIC",
                [VisibleValue.Id("Relic", buy.Entry.Relic.Id.Entry), VisibleValue.Int("Price", buy.Entry.Price),
                 VisibleValue.Int("Count", 1)])
                { Contents = [VisibleContentProjection.ForRelic(buy.Entry.Relic)] },

            CustomEventDecision.UsePotion use => new VisibleOption("USE_POTION",
                [VisibleValue.Id("Potion", use.Potion.Id.Entry), VisibleValue.Int("UseCount", 1)])
                { Contents = [VisibleContentProjection.ForPotion(use.Potion)] },
            CustomEventDecision.Leave => new VisibleOption("LEAVE", []),
            _ => throw new NotSupportedException($"Unsupported fake merchant decision {candidate.GetType().Name}."),
        }).ToImmutableArray()) { CoverageGapIds = gaps.Distinct().ToImmutableArray() };

        void AddContent(string prefix, VisibleContent content)
        {
            context.Add(VisibleValue.Id(prefix + ".Content", content.ContentId));
            foreach (VisibleValue value in content.Values)
                context.Add(value with { Name = prefix + "." + value.Name });
            gaps.AddRange(content.CoverageGapIds);
            foreach (VisibleContent child in content.Children)
                AddContent(prefix + ".Hover." + child.ContentId, child);
        }
    }

    protected override void CalculateVars()
    {
        List<RelicModel> relics = [ModelDb.Relic<FakeAnchor>(), ModelDb.Relic<FakeBloodVial>(),
            ModelDb.Relic<FakeHappyFlower>(), ModelDb.Relic<FakeLeesWaffle>(), ModelDb.Relic<FakeMango>(),
            ModelDb.Relic<FakeOrichalcum>(), ModelDb.Relic<FakeSneckoEye>(),
            ModelDb.Relic<FakeStrikeDummy>(), ModelDb.Relic<FakeVenerableTeaSet>()];
        Rng.Shuffle(relics);
        // Event-room stock does not use the normal merchant-room price hooks.
        var entries = relics.Take(6).Select(r => new MerchantRelicEntry((RelicModel)r.MutableClone(),
            (int)Math.Round(r.MerchantCost * Owner.PlayerRng.Shops.NextFloat(0.85f, 1.15f))));
        Inventory = MerchantInventory.CreateForEvent(Owner, entries);
    }
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() => [];

    public async Task Buy(MerchantRelicEntry entry)
    {
        await _interactionGate.WaitAsync();
        try
        {
            ValidateActive();
            if (!Inventory.Relics.Contains(entry) || entry.Purchased || Owner.Gold < entry.Price)
                throw new InvalidOperationException("This fake relic cannot be purchased.");
            int price = entry.Price;
            await PlayerCmd.LoseGold(price, Owner, GoldLossType.Spent);
            await RelicCmd.Obtain(entry.Relic, Owner);
            entry.MarkPurchased();
            await Hook.AfterItemPurchased(RunState, Owner, entry, price);
        }
        finally { _interactionGate.Release(); }
    }
    public async Task Leave()
    {
        await _interactionGate.WaitAsync();
        try { ValidateActive(); Finish(); }
        finally { _interactionGate.Release(); }
    }
    public async Task FoulPotionThrown(FoulPotion potion)
    {
        await _interactionGate.WaitAsync();
        try
        {
            ValidateActive();
            if (!ReferenceEquals(potion.Owner, Owner) || IsInventoryOpen)
                throw new InvalidOperationException("The fake merchant is unavailable to this potion.");
            StartedFight = true;
            _combatRewards = new[] { (RelicModel)ModelDb.Relic<FakeMerchantsRug>().MutableClone() }
                .Concat(Inventory.Relics.Where(e => !e.Purchased).Select(e => e.Relic))
                .Select(r => (Reward)new RelicReward(r, Owner)).ToArray();
            RequestForcedCombat(() => FakeMerchantEventEncounter.Definition.CreateMonster());
            SuspendForForcedCombat();
        }
        finally { _interactionGate.Release(); }
    }
    private void ValidateActive()
    {
        AssertMutable();
        if (IsFinished || StartedFight || RunState.CurrentRoom is not EventRoom room || !ReferenceEquals(room.Event, this))
            throw new InvalidOperationException("This fake merchant is no longer active.");
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        _interactionGate = new SemaphoreSlim(1, 1);
        _combatRewards = new List<Reward>().AsReadOnly();
        Inventory = null!;
        IsInventoryOpen = false;
        StartedFight = false;
    }
}
