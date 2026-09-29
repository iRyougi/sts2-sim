using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Entities.Gold;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Rewards;
using Sts2Sim.Core.Runs;

namespace Sts2Sim.Core.Models.Events;

public sealed class CrystalSphere : EventModel
{
    public int UncoverFutureCost { get; private set; }
    public CrystalSphereMinigame? Game { get; private set; }
    protected override bool UsesCustomInteraction => true;
    public override bool IsAllowed(IRunState runState) => runState is RunState { CurrentActIndex: > 0 } &&
        runState.Players.All(p => p.Gold >= 100);
    protected override void CalculateVars() => UncoverFutureCost = 50 + Rng.NextInt(1, 50);
    protected override IReadOnlyList<EventOption> GenerateInitialOptions() =>
        [new("UNCOVER_FUTURE", Uncover), new("PAYMENT_PLAN", PaymentPlan)];

    private async Task Uncover()
    {
        await PlayerCmd.LoseGold(UncoverFutureCost, Owner, GoldLossType.Spent);
        StartGame(3);
    }
    private async Task PaymentPlan()
    {
        await CardPileCmd.AddCursesToDeck([ModelDb.Card<Debt>()], Owner);
        StartGame(6);
    }
    private void StartGame(int count)
    {
        Game = new CrystalSphereMinigame(Owner, Rng, count);
        SetOptions([]);
    }
    public async Task RevealAsync(int x, int y, bool big = true)
    {
        AssertMutable();
        if (Game is null || IsFinished) throw new InvalidOperationException("The sphere minigame is not active.");
        await Game.RevealAsync(x, y, big);
        if (Game.DivinationCount == 0)
        {
            if (Game.Rewards.Count > 0) OfferRewards(RewardsSet.CreateCustom(Owner, extraRewards: Game.Rewards));
            Finish();
        }
    }
    protected override void AfterCloned()
    {
        base.AfterCloned();
        UncoverFutureCost = 0;
        Game = null;
    }
}
