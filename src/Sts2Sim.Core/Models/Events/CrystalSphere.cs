using System.Collections.Immutable;
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
    protected override VisibleDecisionMetadata BuildVisibleState() => new([], [
        new("UNCOVER_FUTURE", [VisibleValue.Int("UncoverFutureCost", UncoverFutureCost), VisibleValue.Int("UncoverFutureProphesizeCount", 3)]),
        new("PAYMENT_PLAN", [VisibleValue.Id("CurseTitle", ModelDb.GetId<Debt>().Entry), VisibleValue.Int("PaymentPlanCount", 6), VisibleValue.Int("CurseCount", 1)]) { Contents = [VisibleContentProjection.ForCard(ModelDb.Card<Debt>())] },
    ]);

    protected override VisibleDecisionMetadata BuildCustomVisibleState(IReadOnlyList<CustomEventDecision> candidates)
    {
        CrystalSphereMinigame game = Game ?? throw new InvalidOperationException("The sphere minigame is not active.");
        var context = ImmutableArray.CreateBuilder<VisibleValue>();
        context.Add(VisibleValue.Int("DivinationCount", game.DivinationCount));
        context.Add(VisibleValue.Int("Width", game.Width));
        context.Add(VisibleValue.Int("Height", game.Height));
        // Approved Crystal Sphere-only layout boundary: no future randomized reward IDs.
        context.Add(VisibleValue.Scope("InformationBoundary", "CrystalSphereLayoutException"));
        for (int x = 0; x < game.Width; x++)
            for (int y = 0; y < game.Height; y++)
                context.Add(VisibleValue.Condition($"Cell[{x},{y}].Hidden", game.IsHidden(x, y) ? "true" : "false"));
        int itemIndex = 0;
        foreach (CrystalSphereItem item in game.Items)
        {
            if (item.Position is not { } position) continue;
            string prefix = $"Item[{itemIndex++}]";
            context.Add(VisibleValue.Int($"{prefix}.X", position.X));
            context.Add(VisibleValue.Int($"{prefix}.Y", position.Y));
            context.Add(VisibleValue.Int($"{prefix}.Width", item.Width));
            context.Add(VisibleValue.Int($"{prefix}.Height", item.Height));
            context.Add(VisibleValue.Scope($"{prefix}.Kind", item.Kind.ToString()));
            context.Add(VisibleValue.Condition($"{prefix}.Revealed", item.IsRevealed ? "true" : "false"));
            if (item.Kind == CrystalSphereItemKind.CardReward)
                context.Add(VisibleValue.Scope($"{prefix}.CardRarity", item.CardRarity.ToString()));
            if (item.Kind == CrystalSphereItemKind.Potion)
                context.Add(VisibleValue.Scope($"{prefix}.PotionRarity", item.PotionRarity.ToString()));
            if (item.Kind == CrystalSphereItemKind.Gold)
                context.Add(VisibleValue.Int($"{prefix}.Gold", item.IsBigGold ? 30 : 10));
            if (item.Kind == CrystalSphereItemKind.Curse)
                context.Add(VisibleValue.Id($"{prefix}.Curse", ModelDb.GetId<Doubt>().Entry));
        }
        return new(context.ToImmutable(), candidates.Select(candidate => candidate switch
        {
            CustomEventDecision.RevealSphere reveal => new VisibleOption("REVEAL_SPHERE",
                [VisibleValue.Int("X", reveal.X), VisibleValue.Int("Y", reveal.Y),
                 VisibleValue.Scope("Tool", reveal.Big ? "Big" : "Small"), VisibleValue.Int("DivinationCost", 1)]),
            _ => throw new NotSupportedException($"Unsupported sphere decision {candidate.GetType().Name}."),
        }).ToImmutableArray());
    }

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
