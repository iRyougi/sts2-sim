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
