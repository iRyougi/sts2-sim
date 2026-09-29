using Sts2Sim.Core.Content;
using Sts2Sim.Core.Content.Acts;
using Sts2Sim.Core.Commands;
using Sts2Sim.Core.Combat;
using Sts2Sim.Core.Entities.Cards;
using Sts2Sim.Core.Entities.Creatures;
using Sts2Sim.Core.Entities.Players;
using Sts2Sim.Core.Events;
using Sts2Sim.Core.Map;
using Sts2Sim.Core.Models;
using Sts2Sim.Core.Models.Cards;
using Sts2Sim.Core.Models.Characters;
using Sts2Sim.Core.Models.Monsters;
using Sts2Sim.Core.Models.Powers;
using Sts2Sim.Core.Models.Relics;
using Sts2Sim.Core.Runs;
using Sts2Sim.Core.Rooms;

namespace Sts2Sim.Core.Tests.Content.Acts;

internal sealed class UnderdocksAcceptanceFactAttribute : FactAttribute
{
    public UnderdocksAcceptanceFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("STS2_UNDERDOCKS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set STS2_UNDERDOCKS=1 to run the Underdocks full-run acceptance.";
        }
    }
}

[Collection("ModelDb")]
public sealed class UnderdocksFullRunAcceptanceTests : IDisposable
{
    public UnderdocksFullRunAcceptanceTests()
    {
        ModelDb.ResetForTests();
        ModelDb.Init(ContentRegistry.AllTypes);
    }

    public void Dispose() => ModelDb.ResetForTests();

    [UnderdocksAcceptanceFact]
    public async Task A10Silent_CompletesTwentyRunsThroughEachActOneVariant()
    {
        IReadOnlyList<string> underdocksSeeds = FindSeeds<Underdocks>(20);
        IReadOnlyList<string> overgrowthSeeds = FindSeeds<Overgrowth>(20);

        foreach (string seed in underdocksSeeds.Concat(overgrowthSeeds))
        {
            IReadOnlyList<ActDefinition> acts = ActDefinition.GetRandomList(seed);
            var runState = new RunState(seed, acts, ascensionLevel: 10);
            Player player = Player.CreateForNewRun(ModelDb.Character<Silent>(), runState);
            LoadApprovedSnapshot(player);
            await CreatureCmd.SetMaxAndCurrentHp(player.Creature, 1_000_000_000m);
            runState.AddPlayer(player);

            var decisions = new UnderdocksAcceptanceDecisionSource();
            var driver = new RunDriver(runState, decisions, useAvailablePotions: false);
            RunDriver.Result result = await driver.RunAsync(maxFloors: 300);

            Assert.True(
                result.Won,
                $"Seed {seed} ({acts[0].GetType().Name}) stopped after {result.FloorsVisited} floors " +
                $"in act {runState.CurrentActIndex}, room {DescribeRoom(runState.CurrentRoom)}, " +
                $"with {result.FinalPlayerHp} HP.");
            Assert.IsType<Glory>(runState.Act);
            Assert.Equal(3, result.ActsCleared);
        }
    }

    private static IReadOnlyList<string> FindSeeds<TAct>(int count)
        where TAct : ActDefinition
    {
        var seeds = new List<string>(count);
        for (int candidate = 0; seeds.Count < count; candidate++)
        {
            string seed = $"underdocks-acceptance-{candidate}";
            if (ActDefinition.GetRandomList(seed)[0] is TAct)
            {
                seeds.Add(seed);
            }
        }

        return seeds;
    }

    private static string DescribeRoom(AbstractRoom? room) => room switch
    {
        CombatRoom combat => $"{combat.RoomType}/{combat.EncounterName}",
        null => "none",
        _ => room.GetType().Name,
    };

    private static void LoadApprovedSnapshot(Player player)
    {
        foreach (CardModel card in player.Deck.Cards.ToArray())
        {
            player.Deck.RemoveInternal(card);
        }

        AddSnapshotCard<DefendSilent>(player);
        AddSnapshotCard<DefendSilent>(player);
        AddSnapshotCard<Neutralize>(player, upgraded: true);
        AddSnapshotCard<Survivor>(player);
        AddSnapshotCard<AscendersBane>(player);
        AddSnapshotCard<Backflip>(player);
        AddSnapshotCard<FlickFlack>(player, upgraded: true);
        AddSnapshotCard<Acrobatics>(player);
        AddSnapshotCard<CorrosiveWave>(player, upgraded: true);
        AddSnapshotCard<Prepared>(player, upgraded: true);
        AddSnapshotCard<Backflip>(player);
        AddSnapshotCard<Prepared>(player, upgraded: true);
        AddSnapshotCard<Afterimage>(player, upgraded: true);
        AddSnapshotCard<CalculatedGamble>(player);
        AddSnapshotCard<Anticipate>(player, upgraded: true);
        AddSnapshotCard<LegSweep>(player);
        AddSnapshotCard<Finesse>(player);
        AddSnapshotCard<Reflex>(player, upgraded: true);
        AddSnapshotCard<UltimateStrike>(player);
        AddSnapshotCard<Outbreak>(player);
        AddSnapshotCard<Tactician>(player, upgraded: true);

        foreach (RelicModel relic in player.Relics.ToArray())
        {
            player.RemoveRelicInternal(relic);
        }

        AddSnapshotRelic<PreciseScissors>(player);
        AddSnapshotRelic<PenNib>(player);
        AddSnapshotRelic<Akabeko>(player);
        AddSnapshotRelic<PaelsEye>(player);
        AddSnapshotRelic<MealTicket>(player);
        AddSnapshotRelic<BloodVial>(player);
        AddSnapshotRelic<IceCream>(player);
        // EmptyCage is intentionally omitted: its already-resolved removals are reflected in the deck snapshot.
        AddSnapshotRelic<Whetstone>(player);
        AddSnapshotRelic<StoneCracker>(player);
        AddSnapshotRelic<Vajra>(player);
        AddSnapshotRelic<SneckoSkull>(player);
    }

    private static void AddSnapshotCard<TCard>(Player player, bool upgraded = false)
        where TCard : CardModel
    {
        var card = (TCard)ModelDb.Card<TCard>().MutableClone();
        card.AssignOwner(player);
        if (upgraded)
        {
            card.Upgrade();
        }

        player.Deck.AddInternal(card);
    }

    private static void AddSnapshotRelic<TRelic>(Player player)
        where TRelic : RelicModel
    {
        var relic = (TRelic)ModelDb.Relic<TRelic>().MutableClone();
        relic.AssignOwner(player);
        player.AddRelicInternal(relic);
    }
}

file sealed class UnderdocksAcceptanceDecisionSource : IRunDecisionSource
{
    private static readonly string[] CompletionKeys =
        ["EXIT", "LEAVE", "DONE", "ABSTAIN", "DECLINE", "GIVE_UP"];

    public Task<MapPoint> ChooseMapPointAsync(IReadOnlyList<MapPoint> options) =>
        Task.FromResult(options.OrderBy(point => point.coord.col).First());

    public Task<CombatDecision> ChooseCombatActionAsync(CombatState state)
    {
        if (state.RoundNumber > 200)
        {
            throw new InvalidOperationException(
                $"Combat exceeded 200 rounds against " +
                $"{string.Join('+', state.Enemies.Select(enemy => enemy.Monster?.GetType().Name))}.");
        }

        Player player = state.Players[0];
        bool isInsatiable = state.Enemies.Any(enemy => enemy.Monster is TheInsatiable);
        bool corrosiveWaveIsActive = player.Creature.GetPower<CorrosiveWavePower>() is not null;
        foreach (CardModel card in player.PlayerCombatState!.Hand.Cards
                     .OrderBy(card => isInsatiable
                         ? InsatiablePriority(card, corrosiveWaveIsActive)
                         : card.Type == CardType.Attack ? 0 : card.Type == CardType.Power ? 1 : 2))
        {
            if (!card.CanPlay(out _))
            {
                continue;
            }

            Creature? target = card.TargetType switch
            {
                TargetType.AnyEnemy => state.HittableEnemies.FirstOrDefault(),
                TargetType.AnyAlly or TargetType.AnyPlayer =>
                    CombatTargetCandidates.ForCard(state, player, card.TargetType)
                        .FirstOrDefault(candidate => !ReferenceEquals(candidate, player.Creature))
                    ?? CombatTargetCandidates.ForCard(state, player, card.TargetType).FirstOrDefault(),
                _ => null,
            };
            if (card.TargetType is (TargetType.AnyEnemy or TargetType.AnyAlly or TargetType.AnyPlayer) &&
                target is null)
            {
                continue;
            }

            return Task.FromResult<CombatDecision>(new CombatDecision.PlayCard(card, target));
        }

        return Task.FromResult<CombatDecision>(new CombatDecision.EndTurn());
    }

    public Task<IReadOnlyList<CardModel>> ChooseCardsAsync(CardSelectionRequest request) =>
        Task.FromResult<IReadOnlyList<CardModel>>(request.Candidates
            .OrderBy(card => card is Tactician ? 0 : card is Reflex ? 1 : 2)
            .Take(request.MaxCount)
            .ToArray());

    private static int InsatiablePriority(CardModel card, bool corrosiveWaveIsActive) => card switch
    {
        FranticEscape => 0,
        CorrosiveWave => 1,
        Prepared or Finesse or CalculatedGamble => 2,
        Acrobatics or Backflip when corrosiveWaveIsActive => 3,
        Outbreak => 4,
        _ when card.Type == CardType.Attack => 5,
        _ when card.Type == CardType.Power => 6,
        Acrobatics or Backflip => 7,
        _ => 8,
    };

    public Task<EventOption> ChooseEventOptionAsync(IReadOnlyList<EventOption> options)
    {
        EventOption[] enabled = options.Where(option => option.IsEnabled).ToArray();
        EventOption chosen = enabled.FirstOrDefault(option =>
                CompletionKeys.Any(key => option.Key.Contains(key, StringComparison.OrdinalIgnoreCase)))
            ?? enabled[0];
        return Task.FromResult(chosen);
    }
}
